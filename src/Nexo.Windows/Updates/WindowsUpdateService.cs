using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using Nexo.Core.Updates;

namespace Nexo.Windows.Updates;

/// <summary>Lo que se encontró al mirar si hay versión nueva.</summary>
public readonly record struct UpdateLookup(
    bool Found,
    UpdateManifest? Manifest,
    string Message)
{
    public static UpdateLookup Nothing(string message) => new(false, null, message);

    public static UpdateLookup Offer(UpdateManifest manifest) =>
        new(true, manifest, string.Empty);
}

/// <summary>
/// Diseño D64 — junta las piezas: mira, decide, descarga, prepara y llama al ayudante.
///
/// Aquí no se decide nada nuevo. Cada regla vive en su sitio —
/// <see cref="UpdateCheckPolicy"/> dice si toca mirar y si merece ofrecerse,
/// <see cref="UpdateManifestReader"/> dice si el anuncio vale,
/// <see cref="UpdateSwapPathPolicy"/> dice qué carpetas se pueden tocar — y esto solo las llama en
/// orden. Es a propósito: un orquestador que además opina es un orquestador que hay que probar con
/// la red delante.
///
/// **Nada de esto ocurre solo todavía.** El disparo es manual mientras no se haya visto funcionar
/// sobre una instalación de verdad. Un actualizador que se activa por su cuenta antes de estar
/// comprobado es la peor forma posible de descubrir que tenía un fallo.
/// </summary>
public sealed class WindowsUpdateService(HttpClient? client = null)
{
    private const string Owner = "EXOTARA";
    private const string Repository = "Nexo";

    private readonly HttpClient _client = client ?? CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();

        // GitHub rechaza las peticiones sin identificación de cliente. Sin esto, la consulta
        // devuelve un 403 que parece un problema de permisos y no lo es.
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("Kohana", "1.0"));
        client.Timeout = TimeSpan.FromSeconds(20);

        return client;
    }

    /// <summary>
    /// Mira si hay algo más nuevo. No descarga nada: solo consulta y decide.
    /// </summary>
    public async Task<UpdateLookup> LookForUpdateAsync(
        KohanaVersion current,
        KohanaVersion? skipped,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await _client.GetStringAsync(
                GitHubReleaseReader.LatestReleaseUrl(Owner, Repository), cancellationToken);

            var release = GitHubReleaseReader.Read(json);
            if (!release.IsUsable)
            {
                return UpdateLookup.Nothing(release.Problem);
            }

            // La huella viaja en su propio archivo. Es pequeño, así que se baja entero.
            var checksum = GitHubReleaseReader.ReadChecksum(
                await _client.GetStringAsync(release.ChecksumUrl, cancellationToken));

            var manifest = UpdateManifestReader.Read(
                release.Version,
                release.PackageUrl,
                checksum,
                release.PackageSize.ToString(),
                release.Notes);

            if (!manifest.IsUsable)
            {
                return UpdateLookup.Nothing(manifest.Problem);
            }

            var decision = UpdateCheckPolicy.Evaluate(
                current,
                manifest.Manifest!.Version,
                skipped,
                UpdateCheckPolicy.AcceptsPreReleases(current));

            return decision.ShouldOffer
                ? UpdateLookup.Offer(manifest.Manifest)
                : UpdateLookup.Nothing(Explain(decision.Verdict));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException or InvalidOperationException)
        {
            // Estar sin conexión no es un fallo de Kohana y no debe contarse como tal.
            return UpdateLookup.Nothing("No se pudo consultar si hay una versión nueva.");
        }
    }

    private static string Explain(UpdateVerdict verdict) => verdict switch
    {
        UpdateVerdict.AlreadyCurrent => "Kohana ya está en la última versión.",
        UpdateVerdict.PreReleaseNotWanted => "Solo hay una versión preliminar, y no la usas.",
        UpdateVerdict.Skipped => "Esa versión ya la dejaste pasar.",
        _ => string.Empty
    };

    /// <summary>
    /// Descarga, desempaqueta al lado de la instalación y deja el ayudante listo. Devuelve la ruta
    /// del guion, o vacío con el motivo.
    ///
    /// Lo que **no** hace es cerrar Kohana: eso lo decide quien llama, porque cerrar la aplicación
    /// de alguien es una acción suya y no de una tarea de fondo.
    /// </summary>
    public async Task<(bool Ready, string HelperPath, string Problem)> PrepareAsync(
        UpdateManifest manifest,
        string installFolder,
        string workFolder,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var paths = UpdateSwapPathPolicy.Resolve(installFolder);
        if (!paths.IsSafe)
        {
            return (false, string.Empty, paths.Problem);
        }

        var download = await new WindowsUpdateDownloader(_client)
            .DownloadAsync(manifest, workFolder, progress, cancellationToken);

        if (!download.Ok)
        {
            return (false, string.Empty, download.Problem);
        }

        try
        {
            // Se desempaqueta al lado, nunca encima: mientras esto ocurre la instalación sigue
            // intacta y cancelar no cuesta nada.
            if (Directory.Exists(paths.Staged))
            {
                Directory.Delete(paths.Staged, recursive: true);
            }

            ZipFile.ExtractToDirectory(download.PackagePath, paths.Staged);

            // Un paquete que se desempaqueta pero no trae el ejecutable no sirve, y descubrirlo
            // ahora es gratis: descubrirlo después de mover la instalación, no.
            var staged = Path.Combine(paths.Staged, "Kohana.exe");
            if (!File.Exists(staged))
            {
                Directory.Delete(paths.Staged, recursive: true);
                return (false, string.Empty, "El paquete descargado no contiene Kohana.");
            }

            Directory.CreateDirectory(workFolder);
            var helperPath = Path.Combine(workFolder, "aplicar-actualizacion.ps1");

            await File.WriteAllTextAsync(
                helperPath,
                UpdateHelperScript.Build(
                    paths,
                    Environment.ProcessId,
                    Path.Combine(paths.Install, "Kohana.exe")),
                cancellationToken);

            return (true, helperPath, string.Empty);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return (false, string.Empty, $"No se pudo preparar la actualización: {exception.Message}");
        }
    }

    /// <summary>
    /// Lanza el ayudante y devuelve si arrancó. Quien llama debe cerrar Kohana justo después: el
    /// guion espera a que este proceso termine antes de tocar nada.
    /// </summary>
    public static bool LaunchHelper(string helperPath)
    {
        try
        {
            var start = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Los argumentos van de uno en uno para que una ruta con espacios no se parta en dos.
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-ExecutionPolicy");
            start.ArgumentList.Add("Bypass");
            start.ArgumentList.Add("-File");
            start.ArgumentList.Add(helperPath);

            return Process.Start(start) is not null;
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}
