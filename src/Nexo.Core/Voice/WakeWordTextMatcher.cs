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
            "ohana",
            "kohanna",
            "coana"
        };

    private static readonly HashSet<string> OyeKohanaPhrases =
        new(StringComparer.Ordinal)
        {
            "oye kohana",
            "oi kohana",
            "oy kohana",
            "oye koana",
            "oi koana",
            "oye cohana",
            "oye coana",
            "oye kojana",
            "oye ohana"
        };

    private static readonly HashSet<string> HeyKohanaPhrases =
        new(StringComparer.Ordinal)
        {
            "hey kohana",
            "ey kohana",
            "ei kohana",
            "e kohana",
            "eh kohana",
            "he kohana",
            "ai kohana",
            "ay kohana",
            "ahi kohana",
            "hay kohana",
            "y kohana",
            "hey koana",
            "ey koana",
            "ei koana",
            "e koana",
            "hey cohana",
            "ey cohana",
            "hey coana",
            "ey coana",
            "hey kojana",
            "ey kojana",
            "hey ohana",
            "ey ohana"
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

    public static bool IsMatch(
        string? text,
        WakeWordPhrase phrase,
        WakeWordSensitivity sensitivity = WakeWordSensitivity.Balanced)
    {
        var normalized = Canonicalize(Normalize(text));
        if (normalized.Length == 0)
        {
            return false;
        }

        if (sensitivity == WakeWordSensitivity.Strict)
        {
            return IsStrictMatch(normalized, phrase);
        }

        var balancedMatch = phrase switch
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

        if (balancedMatch || sensitivity != WakeWordSensitivity.High)
        {
            return balancedMatch;
        }

        return phrase switch
        {
            WakeWordPhrase.OyeKohana or WakeWordPhrase.HeyKohana =>
                KohanaShortPhrases.Contains(normalized) ||
                EndsWithApproximateKohana(normalized),
            WakeWordPhrase.Kohana => EndsWithApproximateKohana(normalized),
            _ => false
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

    private static string Canonicalize(string normalized)
    {
        if (normalized.Length == 0)
        {
            return normalized;
        }

        return normalized
            .Replace("ko hana", "kohana", StringComparison.Ordinal)
            .Replace("ko ana", "koana", StringComparison.Ordinal)
            .Replace("co hana", "cohana", StringComparison.Ordinal)
            .Replace("co ana", "coana", StringComparison.Ordinal)
            .Replace("o hana", "ohana", StringComparison.Ordinal);
    }

    private static bool IsStrictMatch(string normalized, WakeWordPhrase phrase) => phrase switch
    {
        WakeWordPhrase.Kohana => normalized == "kohana",
        WakeWordPhrase.OyeKohana => normalized == "oye kohana",
        WakeWordPhrase.HeyKohana => normalized is "hey kohana" or "ey kohana",
        WakeWordPhrase.OyeNexo => normalized == "oye nexo",
        WakeWordPhrase.HeyNexo => normalized is "hey nexo" or "ey nexo",
        _ => normalized == "nexo"
    };

    private static bool EndsWithApproximateKohana(string normalized)
    {
        var finalToken = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();

        return finalToken is not null &&
               (KohanaShortPhrases.Contains(finalToken) ||
                LevenshteinDistance(finalToken, "kohana") <= 2);
    }

    private static int LevenshteinDistance(string left, string right)
    {
        if (left.Length == 0)
        {
            return right.Length;
        }

        if (right.Length == 0)
        {
            return left.Length;
        }

        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];
        for (var column = 0; column <= right.Length; column++)
        {
            previous[column] = column;
        }

        for (var row = 1; row <= left.Length; row++)
        {
            current[0] = row;
            for (var column = 1; column <= right.Length; column++)
            {
                var substitutionCost = left[row - 1] == right[column - 1] ? 0 : 1;
                current[column] = Math.Min(
                    Math.Min(current[column - 1] + 1, previous[column] + 1),
                    previous[column - 1] + substitutionCost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    private static bool IsLegacyCompatibilityPhrase(string normalized) =>
        NexoShortPhrases.Contains(normalized) ||
        OyeNexoPhrases.Contains(normalized) ||
        HeyNexoPhrases.Contains(normalized);
}
