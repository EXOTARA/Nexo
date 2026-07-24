using Nexo.Core.Voice;

namespace Nexo.Core.Vision;

public static class VisualContextPromptPolicy
{
    private static readonly string[] VisualReferences =
    [
        "que es esto",
        "que es este problema",
        "que significa esto",
        "que estoy viendo",
        "por que falla esto",
        "por que no funciona esto",
        "que tiene esto",
        "mira esto",
        "revisa esto",
        "analiza esto",
        "explica esto",
        "que aparece aqui",
        "que hay aqui"
    ];

    public static bool ShouldAcquireVisualContext(string? prompt, bool fromVoice)
    {
        if (!fromVoice || string.IsNullOrWhiteSpace(prompt))
        {
            return false;
        }

        var normalized = SpanishVoiceTranscriptNormalizer.Normalize(prompt);
        return VisualReferences.Any(reference =>
            normalized.Equals(reference, StringComparison.Ordinal) ||
            normalized.StartsWith(reference + " ", StringComparison.Ordinal));
    }
}
