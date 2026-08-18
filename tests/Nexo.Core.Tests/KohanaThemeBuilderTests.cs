using Nexo.Core.Shell;
using Xunit;

namespace Nexo.Core.Tests;

/// <summary>
/// La garantía central del tema derivado del fondo: el acento lo pone una foto que nadie revisó, y
/// aun así el texto tiene que leerse. Estas pruebas son lo que permite afirmarlo sin abrir la
/// aplicación con cada fondo posible.
/// </summary>
public sealed class KohanaThemeBuilderTests
{
    private static readonly RgbColor Text = RgbColor.FromHex(KohanaThemeBuilder.TextPrimaryHex);

    [Fact]
    public void TextStaysReadableOnEverySurfaceForAnyAccentImaginable()
    {
        var random = new Random(20260816);

        for (var attempt = 0; attempt < 2000; attempt++)
        {
            var accent = new RgbColor(
                (byte)random.Next(256),
                (byte)random.Next(256),
                (byte)random.Next(256));

            var theme = KohanaThemeBuilder.FromAccent(accent);

            foreach (var (name, surface) in new[]
                     {
                         ("fondo", theme.Background),
                         ("superficie", theme.Surface),
                         ("superficie elevada", theme.SurfaceRaised),
                         ("franja de navegación", theme.SidebarSurface)
                     })
            {
                Assert.True(
                    ColorMath.ContrastRatio(surface, Text) >= KohanaThemeBuilder.MinimumTextContrast,
                    $"Con el acento {accent.ToHex()}, el texto sobre la {name} " +
                    $"({surface.ToHex()}) bajó a {ColorMath.ContrastRatio(surface, Text):F2}:1.");
            }
        }
    }

    [Fact]
    public void AWhiteAccentIsTheWorstCaseAndStillDoesNotBreakTheText()
    {
        // El caso extremo: teñir con blanco es lo que más acerca las superficies al color del
        // texto. Si algo va a romper la legibilidad, es esto.
        var theme = KohanaThemeBuilder.FromAccent(new RgbColor(255, 255, 255));

        Assert.True(ColorMath.ContrastRatio(theme.SurfaceRaised, Text) >= KohanaThemeBuilder.MinimumTextContrast);
    }

    [Fact]
    public void TheSurfacesKeepTheirDepthOrderSoTheHierarchyStillReads()
    {
        // La tarjeta elevada siempre fue más clara que el fondo. Un tema que aplane eso hace que
        // las tarjetas dejen de leerse como tarjetas.
        var theme = KohanaThemeBuilder.FromAccent(RgbColor.FromHex("#35C58A"));

        Assert.True(
            ColorMath.RelativeLuminance(theme.SurfaceRaised) > ColorMath.RelativeLuminance(theme.Background),
            "La superficie elevada debería seguir siendo más clara que el fondo.");
    }

    [Fact]
    public void EverySurfaceCarriesTheAccentHueSoNothingStaysADifferentColour()
    {
        // Regresión del defecto que se veía en pantalla: con un tema cálido, el panel se teñía pero
        // las tarjetas seguían moradas. La causa era que las superficies partían de grises que no
        // eran neutros —el de las tarjetas tenía el azul a más del doble que el rojo— y ese tono
        // propio ganaba a la mezcla. Comprobarlo por tono, y no por «cambió respecto a antes», es lo
        // que ata el arreglo: da igual cómo se construya, todas tienen que ser del mismo color.
        foreach (var accentHex in new[] { "#35C58A", "#E08020", "#8B6CFF", "#4D8DFF", "#F06CA8" })
        {
            var accent = RgbColor.FromHex(accentHex);
            var expectedHue = ColorMath.Hue(accent);
            var theme = KohanaThemeBuilder.FromAccent(accent);

            foreach (var (name, surface) in new[]
                     {
                         ("fondo", theme.Background),
                         ("superficie", theme.Surface),
                         ("superficie elevada", theme.SurfaceRaised),
                         ("superficie bajo el ratón", theme.SurfaceHover),
                         ("franja de navegación", theme.SidebarSurface),
                         ("campo de entrada", theme.Input),
                         ("borde", theme.Border)
                     })
            {
                // Un gris puro no tiene tono que comparar, y ahí lo correcto es que no lo tenga:
                // solo pasa si hubo que renunciar a toda la saturación para salvar el texto.
                if (ColorMath.Chroma(surface) <= 0.004)
                {
                    continue;
                }

                var difference = Math.Abs(ColorMath.Hue(surface) - expectedHue);
                difference = Math.Min(difference, 360 - difference);

                // Ocho grados y no cero: el fondo vive alrededor de #0B110F, donde los canales
                // valen 11-17, y a esa oscuridad un solo paso entero de 0-255 ya mueve el tono
                // varios grados. Es el suelo del formato, no holgura de diseño. Sigue siendo un
                // orden de magnitud más estricto que el defecto que evita, donde la tarjeta salía
                // azul con un tema naranja: más de noventa grados de diferencia.
                Assert.True(
                    difference <= 8,
                    $"Con el acento {accentHex}, la {name} ({surface.ToHex()}) salió a " +
                    $"{difference:F0}° del tono del tema.");
            }
        }
    }

    [Fact]
    public void TheAccentItselfIsHandedBackUntouched()
    {
        // Teñir superficies es una cosa; el acento que la persona eligió no se retoca.
        var accent = RgbColor.FromHex("#F06CA8");

        Assert.Equal(accent, KohanaThemeBuilder.FromAccent(accent).Accent);
    }

    [Fact]
    public void TheSurfacesActuallyCarryColourInsteadOfComingOutGrey()
    {
        // La otra mitad del contrato: si por proteger el texto la saturación se anulara siempre, los
        // temas dejarían de notarse y esto sería un sistema de temas que no cambia nada.
        var theme = KohanaThemeBuilder.FromAccent(RgbColor.FromHex("#35C58A"));

        Assert.True(
            ColorMath.Chroma(theme.Surface) > 0.01,
            $"La superficie salió prácticamente gris: {theme.Surface.ToHex()}.");
        Assert.True(
            theme.Surface.G > theme.Surface.R,
            "Un tema verde debería dejar el verde por encima del rojo en la superficie.");
    }

    [Fact]
    public void TwoDifferentAccentsProduceVisiblyDifferentSurfaces()
    {
        var green = KohanaThemeBuilder.FromAccent(RgbColor.FromHex("#35C58A"));
        var pink = KohanaThemeBuilder.FromAccent(RgbColor.FromHex("#F06CA8"));

        Assert.NotEqual(green.Surface, pink.Surface);
        Assert.NotEqual(green.Background, pink.Background);
    }
}
