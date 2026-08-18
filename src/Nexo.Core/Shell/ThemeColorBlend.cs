using System.Globalization;

namespace Nexo.Core.Shell;

/// <summary>
/// Diseño D29 — "temas completos": un tema no es solo el color de los botones, es que el panel
/// entero se sienta distinto. En vez de pintar a mano cuatro fondos distintos para cuatro temas
/// —que hay que mantener sincronizados a mano cada vez que cambia el diseño base—, cada tema mezcla
/// una pizca de su acento sobre los mismos tres tonos neutros de siempre. Mezclar poco (unos pocos
/// puntos porcentuales) es lo que separa "un fondo con otro color" de "ya no se lee el texto
/// encima": la mezcla es deliberadamente sutil.
/// </summary>
public static class ThemeColorBlend
{
    public static string Tint(string baseHex, string accentHex, double amount)
    {
        var (baseR, baseG, baseB) = ParseHex(baseHex);
        var (accentR, accentG, accentB) = ParseHex(accentHex);

        var clampedAmount = Math.Clamp(amount, 0, 1);

        return FormatHex(
            Mix(baseR, accentR, clampedAmount),
            Mix(baseG, accentG, clampedAmount),
            Mix(baseB, accentB, clampedAmount));
    }

    private static byte Mix(byte from, byte to, double amount) =>
        (byte)Math.Round(from + ((to - from) * amount), MidpointRounding.AwayFromZero);

    private static (byte R, byte G, byte B) ParseHex(string hex)
    {
        var span = hex.AsSpan().TrimStart('#');
        var r = byte.Parse(span[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var g = byte.Parse(span.Slice(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var b = byte.Parse(span.Slice(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return (r, g, b);
    }

    private static string FormatHex(byte r, byte g, byte b) =>
        $"#{r:X2}{g:X2}{b:X2}";
}
