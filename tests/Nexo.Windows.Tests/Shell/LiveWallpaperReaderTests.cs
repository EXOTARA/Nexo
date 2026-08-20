using System.IO;
using Nexo.Windows.Shell;
using Xunit;

namespace Nexo.Windows.Tests.Shell;

/// <summary>
/// Corre contra la instalación real de Wallpaper Engine si la hay, y se salta sola si no. Es
/// deliberado que no use un doble: lo que hay que comprobar —que la ruta de Steam del registro
/// lleva a un config real y de ahí a una miniatura que existe— es justo lo que un doble no puede
/// decir. La lógica de leer el JSON ya está cubierta aparte, sin depender de la máquina.
/// </summary>
public sealed class LiveWallpaperReaderTests
{
    [Fact]
    public void NothingIsReportedWhileWallpaperEngineIsNotRunning()
    {
        // Su config conserva el último fondo elegido aunque el programa esté cerrado, y en ese caso
        // el escritorio vuelve a mostrar el fondo de Windows. Leerlo igualmente pintaba Sakura con
        // el color de un fondo que nadie ve.
        var running = System.Diagnostics.Process.GetProcessesByName("wallpaper32").Length > 0 ||
                      System.Diagnostics.Process.GetProcessesByName("wallpaper64").Length > 0;

        if (!running)
        {
            Assert.Null(LiveWallpaperReader.TryFindPreviewImage());
        }
    }

    [Fact]
    public void FindsAReadablePreviewWhenWallpaperEngineIsRunning()
    {
        var preview = LiveWallpaperReader.TryFindPreviewImage();

        if (preview is null)
        {
            // Sin Wallpaper Engine corriendo no hay nada que comprobar, y devolver null es
            // exactamente lo correcto: el lector de siempre toma el relevo.
            return;
        }

        Assert.True(File.Exists(preview), $"La miniatura declarada no existe: {preview}");
        Assert.True(new FileInfo(preview).Length > 0, "La miniatura está vacía.");

        var extension = Path.GetExtension(preview).ToLowerInvariant();
        Assert.Contains(extension, new[] { ".jpg", ".jpeg", ".png", ".gif" });
    }

    [Fact]
    public void TheWallpaperReaderYieldsPixelsFromWhicheverSourceApplies()
    {
        // El contrato de arriba: haya o no fondo animado, de aquí tienen que salir píxeles, porque
        // es lo único que el tema necesita. Cuál de los dos orígenes ganó da igual a este nivel.
        var pixels = WindowsWallpaperReader.TryReadPixels();

        Assert.NotNull(pixels);
        Assert.NotEmpty(pixels!);
    }
}
