using Nexo.Core.Shell;
using Nexo.Windows.Shell;
using Xunit;

namespace Nexo.Windows.Tests.Shell;

/// <summary>
/// Estas pruebas leen el fondo de escritorio REAL del equipo donde corren, no una imagen preparada.
/// Es deliberado: lo que puede fallar aquí —un formato que WPF no decodifique, un archivo bloqueado
/// por Windows mientras lo escribe, una ruta del registro que ya no existe— no aparece con una
/// imagen de laboratorio, y el fondo es justo el dato que no controlamos nosotros.
///
/// Por lo mismo no se afirma nada sobre QUÉ colores salen: dependen del fondo que cada quien tenga
/// puesto. Se afirma que lo que sale es utilizable.
/// </summary>
public sealed class WindowsWallpaperReaderTests
{
    [Fact]
    public void ReadingTheRealWallpaperEitherWorksOrDeclinesCleanly()
    {
        var pixels = WindowsWallpaperReader.TryReadPixels();

        if (pixels is null)
        {
            // Válido: un equipo con fondo de color liso o sin fondo accesible. Lo que no vale es
            // lanzar una excepción, que es lo que impediría abrir Sakura.
            return;
        }

        Assert.NotEmpty(pixels);
    }

    [Fact]
    public void TheSampleIsSmallEnoughToProcessOnEveryWallpaperChange()
    {
        if (WindowsWallpaperReader.TryReadPixels() is not { } pixels)
        {
            return;
        }

        // Se decodifica reducido a propósito. Si esto crece a millones, alguien quitó el
        // DecodePixelWidth y cambiar de fondo pasará a dar un tirón visible en la interfaz.
        Assert.True(
            pixels.Count < 60_000,
            $"La muestra del fondo trajo {pixels.Count} píxeles; debería venir reducida.");
    }

    [Fact]
    public void TheRealWallpaperProducesAPaletteThatAddsUpToTheWholeImage()
    {
        if (WindowsWallpaperReader.TryReadPixels() is not { } pixels)
        {
            return;
        }

        var palette = WallpaperPalette.Quantize(pixels);

        Assert.NotEmpty(palette);
        Assert.All(palette, entry => Assert.InRange(entry.Share, 0, 1));
        Assert.True(palette.Sum(entry => entry.Share) <= 1.0 + 1e-9);
    }

    [Fact]
    public void AnAccentDerivedFromTheRealWallpaperIsAlwaysVisibleOnThePanel()
    {
        // El contrato de punta a punta: del fondo que haya puesto en este equipo, o sale un acento
        // que se ve sobre el panel de Sakura, o no sale ninguno. Nunca uno invisible.
        var panel = new RgbColor(0x0C, 0x0E, 0x14);

        if (WindowsWallpaperReader.TryReadPixels() is not { } pixels)
        {
            return;
        }

        var accent = AccentPicker.Pick(WallpaperPalette.Quantize(pixels), panel);
        if (accent is null)
        {
            return;
        }

        Assert.True(
            ColorMath.ContrastRatio(accent.Value, panel) >= AccentPicker.MinimumContrast,
            $"El acento {accent.Value.ToHex()} no se distinguiría del panel.");
    }
}
