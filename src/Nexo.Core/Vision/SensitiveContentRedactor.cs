using System.Text.RegularExpressions;

namespace Nexo.Core.Vision;

/// <summary>
/// Diseño D5.4 (Fase 2 — Kohana Lens) — redacción a nivel de contenido de texto (líneas de OCR,
/// nombres de elementos de UI Automation). Complementa a <see cref="VisionPrivacyPolicy"/>, que
/// excluye ventanas completas por proceso/título: esto asume que la ventana ya pasó esa exclusión
/// y aun así puede mostrar un dato sensible puntual (una contraseña visible en un campo, un número
/// de tarjeta en un formulario). Se redacta solo el fragmento sensible, no la línea completa, para
/// conservar el contexto útil alrededor.
///
/// Prioriza no dejar pasar un dato sensible por encima de evitar falsos positivos — un texto
/// redactado de más es una molestia menor; uno sin redactar es una fuga real.
/// </summary>
public static class SensitiveContentRedactor
{
    public const string Placeholder = "[REDACTADO]";

    private static readonly Regex CardNumberCandidatePattern = new(
        @"\b(?:\d[ -]?){13,19}\b", RegexOptions.Compiled);

    private static readonly Regex SsnPattern = new(
        @"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled);

    private static readonly Regex PasswordFieldPattern = new(
        @"(?i)\b(contrase[ñn]a|password|clave|pwd)\s*[:=]\s*(?<value>\S+)", RegexOptions.Compiled);

    private static readonly Regex TokenCandidatePattern = new(
        @"\b[A-Za-z0-9_\-]{20,}\b", RegexOptions.Compiled);

    private static readonly string[] KnownSecretPrefixes =
    [
        "sk-", "sk_live_", "pk_live_", "ghp_", "gho_", "ghs_", "github_pat_",
        "AKIA", "AIza", "xoxb-", "xoxp-", "xoxa-"
    ];

    public static bool ContainsSensitiveContent(string? text) =>
        !string.IsNullOrEmpty(text) && Redact(text) != text;

    public static string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        var result = PasswordFieldPattern.Replace(
            text,
            match => match.Value.Replace(match.Groups["value"].Value, Placeholder));

        result = RedactCardNumbers(result);
        result = SsnPattern.Replace(result, Placeholder);
        result = RedactSecretTokens(result);

        return result;
    }

    /// <summary>
    /// Diseño D5.4 (extensión) — devuelve las líneas ORIGINALES (sin redactar, para conservar su
    /// posición real en la imagen) que contienen contenido sensible. Pensado para que quien tenga
    /// la captura de pantalla original pueda tapar esas regiones en la propia imagen antes de
    /// enviarla a cualquier proveedor de IA — redactar el texto no oculta los píxeles.
    /// </summary>
    public static IReadOnlyList<OcrTextLine> FindSensitiveLines(OcrResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess
            ? result.Lines.Where(line => ContainsSensitiveContent(line.Text)).ToArray()
            : [];
    }

    /// <summary>Redacta el texto reconocido por OCR, línea por línea.</summary>
    public static OcrResult Redact(OcrResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.IsSuccess)
        {
            return result;
        }

        var lines = result.Lines.Select(line => line with { Text = Redact(line.Text) }).ToArray();
        return result with { FullText = string.Join('\n', lines.Select(line => line.Text)), Lines = lines };
    }

    /// <summary>Redacta el nombre de cada elemento de un árbol de UI Automation ya leído.</summary>
    public static IReadOnlyList<UiAutomationElement> Redact(IReadOnlyList<UiAutomationElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        return elements
            .Select(element => element.Name is null ? element : element with { Name = Redact(element.Name) })
            .ToArray();
    }

    private static string RedactCardNumbers(string text) =>
        CardNumberCandidatePattern.Replace(text, match =>
        {
            var digits = new string(match.Value.Where(char.IsDigit).ToArray());
            return digits.Length is >= 13 and <= 19 && PassesLuhnCheck(digits)
                ? Placeholder
                : match.Value;
        });

    private static string RedactSecretTokens(string text) =>
        TokenCandidatePattern.Replace(text, match =>
            HasKnownSecretPrefix(match.Value) || LooksLikeRandomToken(match.Value)
                ? Placeholder
                : match.Value);

    private static bool HasKnownSecretPrefix(string token) =>
        KnownSecretPrefixes.Any(prefix => token.StartsWith(prefix, StringComparison.Ordinal));

    /// <summary>
    /// Sin prefijo conocido, solo se considera sospechoso un token que mezcla mayúsculas,
    /// minúsculas y dígitos en proporción alta — una palabra normal con un sufijo numérico no
    /// entra aquí, pero "gA7xQ29pLzR4mN8vK3sT" sí.
    /// </summary>
    private static bool LooksLikeRandomToken(string token)
    {
        var digitCount = token.Count(char.IsDigit);
        var upperCount = token.Count(char.IsUpper);
        var lowerCount = token.Count(char.IsLower);
        return digitCount >= token.Length / 6 && upperCount >= token.Length / 6 && lowerCount > 0;
    }

    private static bool PassesLuhnCheck(string digits)
    {
        var sum = 0;
        var alternate = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var digit = digits[i] - '0';
            if (alternate)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }

            sum += digit;
            alternate = !alternate;
        }

        return sum % 10 == 0;
    }
}
