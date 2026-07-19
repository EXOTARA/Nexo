using System.Globalization;
using System.Text;

namespace Nexo.Core.Ai;

public static class AiContextPolicy
{
    private static readonly string[] SystemKeywords =
    [
        "pc",
        "computadora",
        "ordenador",
        "equipo",
        "sistema",
        "cpu",
        "procesador",
        "ram",
        "memoria",
        "gpu",
        "grafica",
        "rendimiento",
        "lenta",
        "lento",
        "lentitud",
        "proceso",
        "disco",
        "almacenamiento",
        "temperatura",
        "consumo",
        "recursos"
    ];

    private static readonly string[] PersonalDevicePhrases =
    [
        "mi pc",
        "mi computadora",
        "mi ordenador",
        "mi equipo",
        "mi sistema",
        "esta pc",
        "esta computadora",
        "este equipo"
    ];

    private static readonly string[] CurrentStatePhrases =
    [
        "como esta",
        "como anda",
        "por que va lenta",
        "por que va lento",
        "por que esta lenta",
        "por que esta lento",
        "se siente lenta",
        "se siente lento",
        "rendimiento actual",
        "uso actual",
        "ahora mismo",
        "estoy usando",
        "tengo libre",
        "tengo disponible",
        "que proceso esta usando",
        "que proceso consume"
    ];

    public static bool ShouldIncludeSystemMetrics(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return false;
        }

        var normalized = Normalize(prompt);
        if (!SystemKeywords.Any(keyword => ContainsWholeWord(normalized, keyword)))
        {
            return false;
        }

        return PersonalDevicePhrases.Any(normalized.Contains) ||
               CurrentStatePhrases.Any(normalized.Contains);
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        return string.Join(' ', builder.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool ContainsWholeWord(string value, string word) =>
        value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Any(item => item.Equals(word, StringComparison.Ordinal));
}
