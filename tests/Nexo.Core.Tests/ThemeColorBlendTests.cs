using Nexo.Core.Shell;
using Xunit;

namespace Nexo.Core.Tests;

public sealed class ThemeColorBlendTests
{
    [Fact]
    public void WithZeroAmountTheResultIsTheBaseColorUnchanged()
    {
        var result = ThemeColorBlend.Tint("#0C0E14", "#35C58A", 0);

        Assert.Equal("#0C0E14", result);
    }

    [Fact]
    public void WithFullAmountTheResultIsTheAccentColor()
    {
        var result = ThemeColorBlend.Tint("#0C0E14", "#35C58A", 1);

        Assert.Equal("#35C58A", result);
    }

    [Fact]
    public void AHalfBlendLandsHalfwayBetweenBaseAndAccent()
    {
        // #00 -> #10 a mitad de camino es #08, sin ambigüedad de redondeo.
        var result = ThemeColorBlend.Tint("#000000", "#101010", 0.5);

        Assert.Equal("#080808", result);
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(2)]
    public void AnOutOfRangeAmountIsClampedInsteadOfProducingAnInvalidColor(double amount)
    {
        var result = ThemeColorBlend.Tint("#0C0E14", "#35C58A", amount);

        Assert.Matches("^#[0-9A-F]{6}$", result);
    }

    [Fact]
    public void ASubtleBlendStaysCloseToTheBaseColor()
    {
        // El uso real: 6-12% de mezcla. Confirma que a esas proporciones el resultado se mueve
        // hacia el acento sin llegar ni de lejos a parecérsele: sigue siendo el tono neutro base
        // con un matiz, no un fondo teñido a todo color.
        var baseChannels = ChannelsOf("#0C0E14");
        var accentChannels = ChannelsOf("#35C58A");
        var resultChannels = ChannelsOf(ThemeColorBlend.Tint("#0C0E14", "#35C58A", 0.08));

        for (var channel = 0; channel < 3; channel++)
        {
            var distanceToBase = Math.Abs(resultChannels[channel] - baseChannels[channel]);
            var distanceToAccent = Math.Abs(resultChannels[channel] - accentChannels[channel]);

            Assert.True(
                distanceToBase < distanceToAccent,
                $"El canal {channel} quedó más cerca del acento que del fondo base.");
        }
    }

    private static int[] ChannelsOf(string hex) =>
    [
        Convert.ToInt32(hex.Substring(1, 2), 16),
        Convert.ToInt32(hex.Substring(3, 2), 16),
        Convert.ToInt32(hex.Substring(5, 2), 16)
    ];
}
