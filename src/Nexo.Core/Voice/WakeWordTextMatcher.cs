using System.Globalization;
using System.Text;

namespace Nexo.Core.Voice;

public static class WakeWordTextMatcher
{
    private static readonly HashSet<string> KohanaShortPhrases =
        new(StringComparer.Ordinal)
        {
            "kohana",
            "koana",
            "cohana",
            "kojana",
            "ohana"
        };

    private static readonly HashSet<string> OyeKohanaPhrases =
        new(StringComparer.Ordinal)
        {
            "oye kohana",
            "oi kohana",
            "oye koana",
            "oi koana",
            "oye cohana",
            "oye kojana"
        };

    private static readonly HashSet<string> HeyKohanaPhrases =
        new(StringComparer.Ordinal)
        {
            "hey kohana",
            "ey kohana",
            "ei kohana",
            "ai kohana",
            "ahi kohana",
            "hey koana",
            "ey koana"
        };

    private static readonly HashSet<string> NexoShortPhrases =
        new(StringComparer.Ordinal)
        {
            "nexo"
        };

    private static readonly HashSet<string> OyeNexoPhrases =
        new(StringComparer.Ordinal)
        {
            "oye nexo",
            "oi nexo"
        };

    private static readonly HashSet<string> HeyNexoPhrases =
        new(StringComparer.Ordinal)
        {
            "hey nexo",
            "ey nexo",
            "ei nexo",
            "ai nexo",
            "ahi nexo",
            "hey neso",
            "ey neso"
        };

    public static bool IsMatch(string? text, WakeWordPhrase phrase)
    {
        var normalized = Normalize(text);
        if (normalized.Length == 0)
        {
            return false;
        }

        return phrase switch
        {
            WakeWordPhrase.OyeKohana =>
                OyeKohanaPhrases.Contains(normalized) ||
                OyeNexoPhrases.Contains(normalized),
            WakeWordPhrase.HeyKohana =>
                HeyKohanaPhrases.Contains(normalized) ||
                HeyNexoPhrases.Contains(normalized),
            WakeWordPhrase.Kohana =>
                KohanaShortPhrases.Contains(normalized) ||
                OyeKohanaPhrases.Contains(normalized) ||
                HeyKohanaPhrases.Contains(normalized) ||
                IsLegacyCompatibilityPhrase(normalized),
            WakeWordPhrase.OyeNexo => OyeNexoPhrases.Contains(normalized),
            WakeWordPhrase.HeyNexo => HeyNexoPhrases.Contains(normalized),
            _ => NexoShortPhrases.Contains(normalized) ||
                 OyeNexoPhrases.Contains(normalized) ||
                 HeyNexoPhrases.Contains(normalized)
        };
    }

    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        return string.Join(
            ' ',
            builder.ToString()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static bool IsLegacyCompatibilityPhrase(string normalized) =>
        NexoShortPhrases.Contains(normalized) ||
        OyeNexoPhrases.Contains(normalized) ||
        HeyNexoPhrases.Contains(normalized);
}
