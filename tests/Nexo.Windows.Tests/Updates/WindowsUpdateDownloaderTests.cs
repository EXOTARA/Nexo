using System.Net;
using System.Security.Cryptography;
using System.Text;
using Nexo.Core.Updates;
using Nexo.Windows.Updates;

namespace Nexo.Windows.Tests.Updates;

/// <summary>
/// Diseño D62 — bajar el paquete y comprobar que es el publicado.
///
/// Lo que se protege aquí no es la descarga sino lo contrario: que un paquete que no cuadra **no
/// llegue a existir** con nombre de bueno. Un archivo que llegó a medias, o una página de error
/// servida con código 200, tiene el mismo aspecto que uno correcto hasta que se abre — y abrirlo es
/// ya sustituir la aplicación.
/// </summary>
public sealed class WindowsUpdateDownloaderTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "kohana-update-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, recursive: true);
            }
        }
        catch (IOException)
        {
            // Un temporal que no se deja borrar no debe hacer fallar la prueba.
        }
    }

    private sealed class StubHandler(HttpStatusCode status, byte[] body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new ByteArrayContent(body)
            });
    }

    private static string HashOf(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static UpdateManifest ManifestFor(byte[] content, string? hash = null, long? size = null) =>
        new(
            new KohanaVersion(0, 9, 6, "beta"),
            new Uri("https://example.invalid/Kohana.zip"),
            hash ?? HashOf(content),
            size ?? content.Length,
            "notas");

    private WindowsUpdateDownloader DownloaderFor(byte[] body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(new HttpClient(new StubHandler(status, body)));

    [Fact]
    public async Task AMatchingPackageIsKept()
    {
        var content = Encoding.UTF8.GetBytes("una versión de Kohana");
        var result = await DownloaderFor(content).DownloadAsync(ManifestFor(content), _folder);

        Assert.True(result.Ok, result.Problem);
        Assert.True(File.Exists(result.PackagePath));
        Assert.Equal(content, await File.ReadAllBytesAsync(result.PackagePath));
    }

    [Fact]
    public async Task APackageThatDoesNotMatchIsRefusedAndDeleted()
    {
        // El caso que justifica toda la clase. Y no basta con devolver un error: si el archivo
        // sobrevive, queda un paquete no verificado a mano de aplicarse por equivocación.
        var content = Encoding.UTF8.GetBytes("algo que no es lo publicado");
        var manifest = ManifestFor(content, hash: new string('a', 64));

        var result = await DownloaderFor(content).DownloadAsync(manifest, _folder);

        Assert.False(result.Ok);
        Assert.Empty(Directory.GetFiles(_folder));
    }

    [Fact]
    public async Task AServerErrorIsNotTreatedAsAPackage()
    {
        var body = Encoding.UTF8.GetBytes("<html>404</html>");
        var result = await DownloaderFor(body, HttpStatusCode.NotFound)
            .DownloadAsync(ManifestFor(body), _folder);

        Assert.False(result.Ok);
        Assert.Empty(Directory.GetFiles(_folder));
    }

    [Fact]
    public async Task SomethingBiggerThanAnnouncedIsCutOff()
    {
        // Sin este corte, un servidor que sirve un flujo sin fin llenaría el disco antes de que
        // nadie comprobara nada.
        var content = new byte[64 * 1024];
        var manifest = ManifestFor(content, size: 8);

        var result = await DownloaderFor(content).DownloadAsync(manifest, _folder);

        Assert.False(result.Ok);
        Assert.Empty(Directory.GetFiles(_folder));
    }

    [Fact]
    public async Task NothingPartialIsLeftBehindUnderTheGoodName()
    {
        // Se descarga a un temporal y solo se renombra tras verificar, así que un fallo nunca deja
        // un paquete incompleto con el nombre del bueno.
        var content = Encoding.UTF8.GetBytes("contenido");
        var manifest = ManifestFor(content, hash: new string('b', 64));

        await DownloaderFor(content).DownloadAsync(manifest, _folder);

        Assert.DoesNotContain(
            Directory.GetFiles(_folder),
            file => Path.GetFileName(file).StartsWith("Kohana-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProgressIsReported()
    {
        var content = new byte[128 * 1024];
        Random.Shared.NextBytes(content);

        var reports = new List<double>();
        var result = await DownloaderFor(content).DownloadAsync(
            ManifestFor(content), _folder, new Progress<double>(reports.Add));

        Assert.True(result.Ok, result.Problem);

        // Progress<T> entrega en el hilo del contexto, así que aquí solo se puede afirmar que el
        // avance nunca se sale de rango; el número exacto de avisos depende del planificador.
        Assert.All(reports, value => Assert.InRange(value, 0, 1));
    }
}
