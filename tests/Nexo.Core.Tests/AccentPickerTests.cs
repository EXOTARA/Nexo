using Nexo.Core.Shell;
using Xunit;

namespace Nexo.Core.Tests;

/// <summary>
/// Estas pruebas describen fondos reales, no colores abstractos: una foto nocturna, un cielo
/// nublado, una captura en blanco y negro. Es donde se decide si el acento sale del fondo o si sale
/// un color que no se distingue del panel.
/// </summary>
public sealed class AccentPickerTests
{
    /// <summary>El fondo del panel de Kohana, contra el que tiene que verse el acento.</summary>
    private static readonly RgbColor Panel = new(0x0C, 0x0E, 0x14);

    [Fact]
    public void AWallpaperWithNoColourAtAllYieldsNoAccent()
    {
        // Una captura en escala de grises. Devolver null deja que quien llama conserve el tema que
        // ya tenía, en vez de teñir la aplicación de un gris que no representa nada.
        var greyscale = new[]
        {
            new PaletteColor(new RgbColor(18, 18, 18), 0.5),
            new PaletteColor(new RgbColor(128, 128, 128), 0.3),
            new PaletteColor(new RgbColor(240, 240, 240), 0.2)
        };

        Assert.Null(AccentPicker.Pick(greyscale, Panel));
    }

    [Fact]
    public void AnEmptyPaletteYieldsNoAccent()
    {
        Assert.Null(AccentPicker.Pick([], Panel));
    }

    [Fact]
    public void ADarkColourIsBrightenedInsteadOfBeingThrownAway()
    {
        // Encontrado con un fondo real: una imagen oscura con cintas naranjas y magentas. Sus
        // colores tienen croma de sobra pero su contraste contra el panel no llega, y rechazarlos
        // hacía que Kohana concluyera que ese fondo "no tiene color" — cuando cualquiera diría que
        // es naranja y morado. Los fondos oscuros son el caso normal, no el raro.
        var darkOrange = RgbColor.FromHex("#8C2A00");
        Assert.True(ColorMath.ContrastRatio(darkOrange, Panel) < AccentPicker.MinimumContrast);

        var accent = AccentPicker.Pick([new PaletteColor(darkOrange, 0.9)], Panel);

        Assert.NotNull(accent);
        Assert.True(
            ColorMath.ContrastRatio(accent!.Value, Panel) >= AccentPicker.MinimumContrast,
            "El acento aclarado debería cumplir el contraste mínimo.");
    }

    [Fact]
    public void BrighteningKeepsTheHueSoTheThemeStillMatchesTheWallpaper()
    {
        // Aclarar no puede convertir un naranja en un gris claro: si se pierde el tono, el tema
        // deja de parecerse al fondo y toda la función pierde el sentido.
        var darkOrange = RgbColor.FromHex("#8C2A00");

        var accent = AccentPicker.Pick([new PaletteColor(darkOrange, 0.9)], Panel)!.Value;

        Assert.True(accent.R > accent.G && accent.G > accent.B, "Debería seguir siendo un naranja.");
        Assert.True(
            ColorMath.Chroma(accent) >= AccentPicker.MinimumChroma,
            "Aclarar no debería desteñir el color por debajo del mínimo de croma.");
    }

    [Fact]
    public void NearBlackIsRejectedEvenWhenItIsTheBulkOfTheImage()
    {
        // Una foto nocturna: casi todo negro, con un letrero de neón ocupando muy poco. El acento
        // correcto es el neón, no el negro.
        var nightPhoto = new[]
        {
            new PaletteColor(new RgbColor(6, 4, 10), 0.82),
            new PaletteColor(new RgbColor(252, 250, 255), 0.14),
            new PaletteColor(new RgbColor(0xFF, 0x2D, 0x95), 0.04)
        };

        Assert.Equal(new RgbColor(0xFF, 0x2D, 0x95), AccentPicker.Pick(nightPhoto, Panel));
    }

    [Fact]
    public void AVividMinorityBeatsAWashedOutMajority()
    {
        // Un cielo nublado que ocupa la mayor parte contra un tejado rojo pequeño: gana el tejado,
        // que es lo que una persona diría que "da el color" a la foto.
        var skyAndRoof = new[]
        {
            new PaletteColor(new RgbColor(178, 190, 200), 0.72),
            new PaletteColor(new RgbColor(214, 66, 52), 0.09)
        };

        Assert.Equal(new RgbColor(214, 66, 52), AccentPicker.Pick(skyAndRoof, Panel));
    }

    [Fact]
    public void ASpeckOfColourTooSmallToNameIsNotTheAccent()
    {
        // Contrapeso del caso anterior. Ampliar la paleta metió también cubos diminutos, y sin un
        // suelo de proporción unos pocos píxeles con halo de compresión sobre una foto en blanco y
        // negro se convertirían en el color de toda la aplicación.
        var almostGreyscale = new[]
        {
            new PaletteColor(new RgbColor(200, 200, 200), 0.95),
            new PaletteColor(new RgbColor(255, 0, 0), 0.001)
        };

        Assert.Null(AccentPicker.Pick(almostGreyscale, Panel));
    }

    [Fact]
    public void BetweenTwoEquallyVividColoursTheLargerAreaWins()
    {
        var palette = new[]
        {
            new PaletteColor(new RgbColor(0x20, 0x80, 0xE0), 0.40),
            new PaletteColor(new RgbColor(0xE0, 0x80, 0x20), 0.05)
        };

        Assert.Equal(new RgbColor(0x20, 0x80, 0xE0), AccentPicker.Pick(palette, Panel));
    }

    [Fact]
    public void TheChosenAccentIsAlwaysVisibleAgainstTheSurfaceItWasPickedFor()
    {
        // La garantía que importa, comprobada sobre cientos de paletas al azar en vez de sobre los
        // pocos casos que se me ocurrieran a mano. Esta prueba es la que destapó el defecto del
        // filtro por claridad.
        var random = new Random(20260816);

        for (var attempt = 0; attempt < 500; attempt++)
        {
            var palette = Enumerable.Range(0, 6)
                .Select(_ => new PaletteColor(
                    new RgbColor(
                        (byte)random.Next(256),
                        (byte)random.Next(256),
                        (byte)random.Next(256)),
                    random.NextDouble()))
                .ToArray();

            if (AccentPicker.Pick(palette, Panel) is not RgbColor accent)
            {
                continue;
            }

            Assert.True(
                ColorMath.ContrastRatio(accent, Panel) >= AccentPicker.MinimumContrast,
                $"El acento {accent.ToHex()} no se distinguiría del panel.");
            Assert.True(
                ColorMath.Chroma(accent) >= AccentPicker.MinimumChroma,
                $"El acento {accent.ToHex()} es prácticamente un gris.");
        }
    }

    [Fact]
    public void TheSurfaceIsRespectedSoALightPanelGetsADarkenedAccent()
    {
        // El mismo color no sirve sobre cualquier fondo. Un amarillo pálido se ve sobre un panel
        // oscuro y desaparece sobre uno claro: ahí hay que oscurecerlo, no aclararlo.
        var paleYellow = new RgbColor(0xF2, 0xE6, 0x8A);
        var palette = new[] { new PaletteColor(paleYellow, 0.6) };
        var lightPanel = new RgbColor(0xFA, 0xFA, 0xFA);

        Assert.Equal(paleYellow, AccentPicker.Pick(palette, Panel));

        var onLight = AccentPicker.Pick(palette, lightPanel);
        Assert.NotNull(onLight);
        Assert.True(
            ColorMath.ContrastRatio(onLight!.Value, lightPanel) >= AccentPicker.MinimumContrast,
            "Sobre un panel claro, el acento debería haberse oscurecido hasta verse.");
    }
}
