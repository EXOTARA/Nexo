using Nexo.Core.Ai;

namespace Nexo.Core.Vision;

/// <summary>
/// Diseño D5.7 (Fase 2 — Sakura Lens) — decide qué resaltar en pantalla a partir de la respuesta
/// de la IA: no hay un mecanismo de citas/referencias estructuradas todavía (eso requeriría pedirle
/// a la IA una salida estructurada, un cambio mayor de prompt), así que esta primera versión usa
/// una heurística honesta y simple — si el texto de una línea de OCR o el nombre de un elemento de
/// UI Automation aparece tal cual dentro de la respuesta, se resalta esa región. Puede fallar en
/// ambas direcciones (no resaltar algo relevante que la IA parafraseó, o resaltar una coincidencia
/// casual) — es guía visual aproximada, no una prueba de que "esto es exactamente a lo que se
/// refiere la IA".
///
/// Función pura: no captura nada, no llama a la IA. Espera que <paramref name="redactedOcr"/> y
/// <paramref name="redactedElements"/> ya hayan pasado por <see cref="SensitiveContentRedactor"/>
/// — un placeholder "[REDACTADO]" nunca genera un resaltado.
/// </summary>
public static class LensHighlightMatcher
{
    private const int MinimumMatchLength = 4;

    public static IReadOnlyList<LensHighlightRegion> FindMatches(
        string? answerText,
        OcrResult redactedOcr,
        IReadOnlyList<UiAutomationElement> redactedElements,
        int windowLeft,
        int windowTop)
    {
        ArgumentNullException.ThrowIfNull(redactedOcr);
        ArgumentNullException.ThrowIfNull(redactedElements);

        if (string.IsNullOrWhiteSpace(answerText) || !redactedOcr.IsSuccess)
        {
            return [];
        }

        var normalizedAnswer = VisionIntentPolicy.Normalize(answerText);
        var regions = new List<LensHighlightRegion>();

        foreach (var line in redactedOcr.Lines)
        {
            if (IsMatch(line.Text, normalizedAnswer))
            {
                regions.Add(new LensHighlightRegion(
                    windowLeft + line.Left, windowTop + line.Top, line.Width, line.Height));
            }
        }

        foreach (var element in redactedElements)
        {
            // Las coordenadas de UI Automation ya son absolutas de pantalla (a diferencia de las
            // líneas de OCR, relativas a la captura) — no se suma windowLeft/windowTop de nuevo.
            if (IsMatch(element.Name, normalizedAnswer))
            {
                regions.Add(new LensHighlightRegion(
                    element.Left, element.Top, element.Width, element.Height));
            }
        }

        return regions.Distinct().ToArray();
    }

    private static bool IsMatch(string? candidateText, string normalizedAnswer)
    {
        if (string.IsNullOrWhiteSpace(candidateText) ||
            candidateText.Contains(SensitiveContentRedactor.Placeholder, StringComparison.Ordinal))
        {
            return false;
        }

        var normalizedCandidate = VisionIntentPolicy.Normalize(candidateText);
        return normalizedCandidate.Length >= MinimumMatchLength &&
            normalizedAnswer.Contains(normalizedCandidate, StringComparison.Ordinal);
    }
}
