using Nexo.Core.Shell;
using Xunit;

namespace Nexo.Core.Tests;

/// <summary>
/// Se comprueba contra valores publicados de WCAG, no contra lo que devuelva la implementación: si
/// estas fórmulas se desvían, las garantías de legibilidad del tema derivado del fondo dejan de
/// significar nada y nadie se daría cuenta hasta ver un acento invisible en pantalla.
/// </summary>
public sealed class ColorMathTests
{
    private static readonly RgbColor White = new(255, 255, 255);
    private static readonly RgbColor Black = new(0, 0, 0);

    [Fact]
    public void BlackAgainstWhiteIsTheMaximumContrastOfTwentyOne()
    {
        Assert.Equal(21, ColorMath.ContrastRatio(Black, White), precision: 2);
    }

    [Fact]
    public void AColorAgainstItselfHasNoContrast()
    {
        var color = new RgbColor(0x8B, 0x6C, 0xFF);

        Assert.Equal(1, ColorMath.ContrastRatio(color, color), precision: 6);
    }

    [Fact]
    public void ContrastIsTheSameInEitherDirection()
    {
        var background = new RgbColor(0x0C, 0x0E, 0x14);
        var text = new RgbColor(0xDF, 0xE1, 0xEA);

        Assert.Equal(
            ColorMath.ContrastRatio(background, text),
            ColorMath.ContrastRatio(text, background),
            precision: 9);
    }

    [Fact]
    public void GreenWeighsFarMoreThanBlueInRelativeLuminance()
    {
        // No es un detalle: un azul puro y un verde puro tienen el MISMO croma, así que si esto no
        // se cumple, en algún punto se está usando la medida equivocada para decidir visibilidad.
        var pureGreen = new RgbColor(0, 255, 0);
        var pureBlue = new RgbColor(0, 0, 255);

        Assert.Equal(ColorMath.Chroma(pureGreen), ColorMath.Chroma(pureBlue), precision: 9);
        Assert.True(
            ColorMath.RelativeLuminance(pureGreen) > ColorMath.RelativeLuminance(pureBlue) * 5,
            "El verde debería pesar muchísimo más que el azul en luminancia relativa.");
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(255, 255, 255)]
    [InlineData(128, 128, 128)]
    public void AnyShadeOfGreyHasNoChroma(byte r, byte g, byte b)
    {
        Assert.Equal(0, ColorMath.Chroma(new RgbColor(r, g, b)), precision: 9);
    }

    [Fact]
    public void APureHueHasFullChroma()
    {
        Assert.Equal(1, ColorMath.Chroma(new RgbColor(255, 0, 0)), precision: 9);
    }

    [Fact]
    public void ANearWhiteHasAlmostNoChromaEvenThoughItsChannelsDiffer()
    {
        // El caso concreto que rompía la elección de acento: la saturación HSL de este color vale
        // 1.0, el máximo posible. Su croma —lo que se corresponde con lo que se ve— es casi cero.
        var nearWhite = new RgbColor(252, 250, 255);

        Assert.True(
            ColorMath.Chroma(nearWhite) < 0.05,
            $"Un blanco casi puro no debería tener croma apreciable, dio {ColorMath.Chroma(nearWhite)}.");
    }

    [Fact]
    public void ChromaNeverLeavesTheZeroToOneRange()
    {
        var random = new Random(20260816);

        for (var attempt = 0; attempt < 500; attempt++)
        {
            var chroma = ColorMath.Chroma(new RgbColor(
                (byte)random.Next(256),
                (byte)random.Next(256),
                (byte)random.Next(256)));

            Assert.InRange(chroma, 0, 1);
        }
    }
}
