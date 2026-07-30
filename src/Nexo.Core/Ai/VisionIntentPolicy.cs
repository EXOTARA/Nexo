using System.Globalization;
using System.Text;

namespace Nexo.Core.Ai;

public static class VisionIntentPolicy
{
    private static readonly string[] TechnicalTerms =
    [
        "error",
        "falla",
        "fallo",
        "excepcion",
        "exception",
        "advertencia",
        "warning",
        "compila",
        "compilar",
        "compilacion",
        "build",
        "terminal",
        "consola",
        "powershell",
        "cmd",
        "stack trace",
        "codigo",
        "archivo",
        "linea",
        "cs0",
        "no se puede convertir",
        "no se encontro",
        "no existe en el contexto"
    ];

    public static AiRequestMode Resolve(string? prompt, bool hasImages)
    {
        if (!hasImages)
        {
            return AiRequestMode.Standard;
        }

        var normalized = Normalize(prompt);
        if (TechnicalTerms.Any(term =>
                normalized.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            return AiRequestMode.VisionTechnicalDiagnostic;
        }

        return AiRequestMode.VisionGeneral;
    }

    /// <summary>
    /// Diseño D5.7 — se hizo público para que <c>Nexo.Core.Vision.LensHighlightMatcher</c> compare
    /// texto con el mismo criterio (minúsculas, sin acentos) que ya usa esta clase, en vez de
    /// duplicar la normalización.
    /// </summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
