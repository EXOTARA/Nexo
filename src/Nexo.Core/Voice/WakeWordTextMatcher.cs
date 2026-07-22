using System.Globalization;
using System.Text;

namespace Nexo.Core.Voice;

public static class WakeWordTextMatcher
{
    public static bool IsMatch(string? text, WakeWordPhrase phrase)
    {
        var normalized = Normalize(text);
        return phrase switch
        {
            WakeWordPhrase.OyeNexo => normalized is "oye nexo" or "oi nexo",
            WakeWordPhrase.HeyNexo => normalized is
                "hey nexo" or
                "ey nexo" or
                "ei nexo" or
                "ai nexo" or
                "ahi nexo" or
                "hey neso" or
                "ey neso",
            _ => normalized is
                "nexo" or
                "oye nexo" or
                "oi nexo" or
                "hey nexo" or
                "ey nexo" or
                "ei nexo" or
                "ai nexo" or
                "ahi nexo"
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
}
