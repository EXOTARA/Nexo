using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using Nexo.Core.Updates;

namespace Nexo.Windows.Updates;

/// <summary>Dónde quedó la descarga, o por qué no sirve.</summary>
public readonly record struct UpdateDownloadResult(bool Ok, string PackagePath, string Problem)
{
    public static UpdateDownloadResult Succeeded(string path) => new(true, path, string.Empty);

    public static UpdateDownloadResult Failed(string problem) => new(false, string.Empty, problem);
}

/// <summary>
/// Diseño D62 — baja el paquete y comprueba que es el publicado.
///
/// La comprobación no es un extra al final: es la razón de que esta clase exista. Un archivo que
/// llegó a medias, por una conexión que se cortó o por un servidor que devolvió una página de
/// error con código 200, tiene exactamente el mismo aspecto que uno bueno hasta que se abre. Y
/// abrirlo es ya sustituir la aplicación.
///
/// **Se descarga siempre a un archivo temporal aparte.** Nunca sobre un nombre definitivo: si el
/// proceso muere a mitad, lo que queda es un temporal que se limpia, y no un paquete incompleto con
/// el nombre del bueno esperando a que alguien lo aplique.
///
/// **La huella se calcula mientras se escribe**, no releyendo el archivo después: ahorra leer cien
/// megas dos veces, y sobre todo comprueba exactamente los bytes que se escribieron en vez de una
/// segunda lectura que podría diferir.
/// </summary>
public sealed class WindowsUpdateDownloader(HttpClient? client = null)
{
    private readonly HttpClient _client = client ?? new HttpClient();

    /// <summary>
    /// Tolerancia sobre el tamaño anunciado. Existe porque el anuncio lo escribe una persona o un
    /// script y puede quedarse corto por unos bytes; lo que se quiere cortar es una descarga que se
    /// dispara, no una que difiere en un kilobyte.
    /// </summary>
    private const double SizeTolerance = 1.05;

    public async Task<UpdateDownloadResult> DownloadAsync(
        UpdateManifest manifest,
        string destinationFolder,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFolder);

        var temporary = Path.Combine(
            destinationFolder, $"kohana-update-{Guid.NewGuid():N}.part");

        try
        {
            Directory.CreateDirectory(destinationFolder);
            DiscardOldPackages(destinationFolder);

            string fingerprint;
            using (var response = await _client.GetAsync(
                       manifest.PackageUrl,
                       HttpCompletionOption.ResponseHeadersRead,
                       cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                {
                    return UpdateDownloadResult.Failed(
                        $"El servidor respondió {(int)response.StatusCode} al pedir la descarga.");
                }

                fingerprint = await WriteAndHashAsync(
                    response, temporary, manifest, progress, cancellationToken);
            }

            if (!UpdateManifestReader.MatchesFingerprint(manifest.Sha256, fingerprint))
            {
                // No se conserva para reintentar ni se dice qué huella salió: lo que hay es un
                // archivo que no es el publicado, y guardarlo solo da ocasión de aplicarlo por error.
                Delete(temporary);
                return UpdateDownloadResult.Failed(
                    "Lo descargado no coincide con lo publicado. No se va a instalar.");
            }

            var packagePath = Path.Combine(
                destinationFolder, $"Sakura-{manifest.Version}.zip");

            // Una descarga anterior del mismo nombre se sustituye: ya se verificó la de ahora.
            Delete(packagePath);
            File.Move(temporary, packagePath);

            return UpdateDownloadResult.Succeeded(packagePath);
        }
        catch (OperationCanceledException)
        {
            Delete(temporary);
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException or
                UnauthorizedAccessException or NotSupportedException)
        {
            Delete(temporary);
            return UpdateDownloadResult.Failed($"No se pudo descargar la actualización: {exception.Message}");
        }
    }

    private static async Task<string> WriteAndHashAsync(
        HttpResponseMessage response,
        string temporary,
        UpdateManifest manifest,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var limit = (long)(manifest.SizeInBytes * SizeTolerance);

        using var hash = SHA256.Create();
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = new FileStream(
            temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true);

        var buffer = new byte[64 * 1024];
        long written = 0;

        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            written += read;

            // Se corta en cuanto se pasa, no al final. Sin esto, un servidor que sirve un flujo sin
            // fin llenaría el disco antes de que nadie comprobara nada.
            if (written > limit)
            {
                throw new IOException("La descarga ocupa más de lo anunciado.");
            }

            hash.TransformBlock(buffer, 0, read, null, 0);
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);

            if (manifest.SizeInBytes > 0)
            {
                progress?.Report(Math.Min(1.0, (double)written / manifest.SizeInBytes));
            }
        }

        hash.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(hash.Hash!).ToLowerInvariant();
    }

    /// <summary>
    /// Tira los paquetes de intentos anteriores antes de bajar uno nuevo.
    ///
    /// El guión borra el suyo al terminar bien, pero un intento que **no** llega al final no borra
    /// nada — y cada uno pesa cien megas. Tras las dos primeras pruebas en vivo quedaron ciento
    /// noventa megas en el disco de Adler sin que nadie los reclamara. Se limpia al empezar y no al
    /// fallar, porque el momento del fallo es justo cuando menos se puede confiar en que se ejecute
    /// nada más.
    ///
    /// Solo se tocan archivos con el nombre que pone esta clase. La carpeta es de Sakura, pero
    /// borrar por comodín lo que haya dentro es cómo se acaba llevándose algo que no era tuyo.
    /// </summary>
    private static void DiscardOldPackages(string folder)
    {
        try
        {
            foreach (var stale in Directory.EnumerateFiles(folder, "Sakura-*.zip"))
            {
                Delete(stale);
            }

            foreach (var stale in Directory.EnumerateFiles(folder, "kohana-update-*.part"))
            {
                Delete(stale);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // No poder limpiar no impide descargar; solo deja el disco más lleno de lo necesario.
        }
    }

    private static void Delete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Un temporal que no se puede borrar ahora no justifica romper el aviso de lo que de
            // verdad pasó. Windows lo limpia con el resto de temporales.
        }
    }
}
