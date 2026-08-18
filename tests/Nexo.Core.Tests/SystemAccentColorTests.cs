using Nexo.Core.Shell;
using Xunit;

namespace Nexo.Core.Tests;

/// <summary>
/// El DWORD de HKCU\Software\Microsoft\Windows\DWM\AccentColor viene empaquetado como ABGR: el
/// byte más bajo es R, no A. Confundir el orden —tratarlo como RGBA o ARGB, los órdenes más
/// habituales— produciría un acento con los canales de color cambiados, no una excepción, así que
/// nadie lo notaría hasta comparar contra Personalización a simple vista.
/// </summary>
public sealed class SystemAccentColorTests
{
    [Fact]
    public void ReadsRedGreenBlueFromTheLowestThreeBytesInThatOrder()
    {
        // 0xFF3388CC: alfa=FF (se ignora), azul=33, verde=88, rojo=CC — el byte más bajo.
        var hex = SystemAccentColor.ToHex(0xFF3388CCu);

        Assert.Equal("#CC8833", hex);
    }

    [Fact]
    public void IgnoresTheAlphaByteEntirely()
    {
        var withAlpha = SystemAccentColor.ToHex(0xAA112233u);
        var withoutAlpha = SystemAccentColor.ToHex(0x00112233u);

        Assert.Equal(withAlpha, withoutAlpha);
        Assert.Equal("#332211", withAlpha);
    }

    [Fact]
    public void ProducesAUppercaseSixDigitHexWithLeadingHash()
    {
        var hex = SystemAccentColor.ToHex(0x00000005u);

        Assert.Equal("#050000", hex);
        Assert.Matches("^#[0-9A-F]{6}$", hex);
    }
}
