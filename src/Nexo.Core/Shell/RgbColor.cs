using System.Globalization;

namespace Nexo.Core.Shell;

/// <summary>
/// Un color sin dependencias de WPF, para que toda la matemática de color viva en Nexo.Core y se
/// pueda probar sin una ventana de por medio.
/// </summary>
public readonly record struct RgbColor(byte R, byte G, byte B)
{
    public static RgbColor FromHex(string hex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hex);

        var span = hex.AsSpan().Trim().TrimStart('#');
        if (span.Length != 6)
        {
            throw new FormatException($"Se esperaba un color de 6 dígitos hexadecimales, llegó «{hex}».");
        }

        return new RgbColor(
            byte.Parse(span[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(span.Slice(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(span.Slice(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";
}
