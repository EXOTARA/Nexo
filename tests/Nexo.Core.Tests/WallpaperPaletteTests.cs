using Nexo.Core.Shell;
using Xunit;

namespace Nexo.Core.Tests;

public sealed class WallpaperPaletteTests
{
    [Fact]
    public void AnEmptyImageProducesAnEmptyPalette()
    {
        Assert.Empty(WallpaperPalette.Quantize([]));
    }

    [Fact]
    public void AColourfulMinoritySurvivesAnImageDominatedByOneNeutral()
    {
        // Regresión de un defecto encontrado con un fondo real: una imagen casi toda blanca con unas
        // alas carmesí. Al recortar la paleta a los más frecuentes, los primeros puestos eran todos
        // tonos de blanco —el degradado los reparte en cubos vecinos, cada uno grande— y el carmesí,
        // que es lo que cualquiera diría que define la imagen, no llegaba a estar en la lista.
        var pixels = new List<RgbColor>();

        // Muchos blancos ligeramente distintos: caen en cubos separados y copan las primeras plazas.
        for (var i = 0; i < 24; i++)
        {
            pixels.AddRange(Enumerable.Repeat(new RgbColor((byte)(220 + i), (byte)(220 + i), (byte)(225 + i)), 300));
        }

        pixels.AddRange(Enumerable.Repeat(new RgbColor(0xC4, 0x10, 0x4C), 900));

        var palette = WallpaperPalette.Quantize(pixels);

        Assert.Contains(palette, entry => ColorMath.Chroma(entry.Color) >= AccentPicker.MinimumChroma);
    }

    [Fact]
    public void ASolidColourCollapsesToASingleEntryCoveringTheWholeImage()
    {
        var pixels = Enumerable.Repeat(new RgbColor(0x35, 0xC5, 0x8A), 500).ToArray();

        var palette = WallpaperPalette.Quantize(pixels);

        var only = Assert.Single(palette);
        Assert.Equal(1.0, only.Share, precision: 6);
        Assert.Equal(new RgbColor(0x35, 0xC5, 0x8A), only.Color);
    }

    [Fact]
    public void TheMostFrequentColourComesFirst()
    {
        var pixels = Enumerable.Repeat(new RgbColor(200, 20, 20), 90)
            .Concat(Enumerable.Repeat(new RgbColor(20, 20, 200), 10))
            .ToArray();

        var palette = WallpaperPalette.Quantize(pixels);

        Assert.Equal(2, palette.Count);
        Assert.Equal(0.9, palette[0].Share, precision: 6);
        Assert.True(palette[0].Color.R > palette[0].Color.B, "El rojo dominante debía quedar primero.");
    }

    [Fact]
    public void ASmoothGradientCollapsesIntoAHandfulOfFamilies()
    {
        // El caso real de un degradado: 256 tonos donde casi ningún píxel repite color exacto. Lo
        // que importa no es que salga un número concreto de familias, sino que salgan MUCHÍSIMAS
        // menos que tonos de entrada — sin agrupar, la paleta no significaría nada.
        var pixels = Enumerable.Range(0, 256)
            .Select(value => new RgbColor((byte)value, 50, 50))
            .ToArray();

        var palette = WallpaperPalette.Quantize(pixels, maximumColors: 64);

        Assert.True(
            palette.Count <= 32,
            $"256 tonos deberían agruparse en 32 familias o menos, salieron {palette.Count}.");
    }

    [Fact]
    public void TheReturnedColourIsTheRealAverageNotTheCentreOfTheGrid()
    {
        // Dos tonos que caen en el mismo cubo (96..103): el resultado debe ser su promedio real,
        // no el centro ni el borde del cubo.
        var pixels = new[]
        {
            new RgbColor(97, 50, 50),
            new RgbColor(101, 50, 50)
        };

        var palette = WallpaperPalette.Quantize(pixels);

        Assert.Equal(99, Assert.Single(palette).Color.R);
    }

    [Fact]
    public void TwoShadesTooFarApartAreNotForcedIntoTheSameFamily()
    {
        // La otra mitad del contrato: agrupar es útil, fundir colores distintos no lo es.
        var pixels = new[]
        {
            new RgbColor(40, 50, 50),
            new RgbColor(200, 50, 50)
        };

        Assert.Equal(2, WallpaperPalette.Quantize(pixels).Count);
    }

    [Fact]
    public void TheSharesOfAllReturnedColoursNeverExceedTheWholeImage()
    {
        var random = new Random(20260816);
        var pixels = Enumerable.Range(0, 4000)
            .Select(_ => new RgbColor(
                (byte)random.Next(256),
                (byte)random.Next(256),
                (byte)random.Next(256)))
            .ToArray();

        var palette = WallpaperPalette.Quantize(pixels, maximumColors: 8);

        Assert.True(palette.Sum(entry => entry.Share) <= 1.0 + 1e-9);
        Assert.True(palette.Count <= 8);
    }

    [Fact]
    public void TheCallerDecidesHowManyColoursComeBack()
    {
        var pixels = Enumerable.Range(0, 40)
            .Select(index => new RgbColor((byte)(index * 6), 40, 200))
            .ToArray();

        Assert.True(WallpaperPalette.Quantize(pixels, maximumColors: 3).Count <= 3);
    }
}
