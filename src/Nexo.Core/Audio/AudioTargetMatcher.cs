using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Nexo.Core.Audio;

public static partial class AudioTargetMatcher
{
    public static bool IsMatch(
        string? target,
        string? processName,
        string? displayName)
    {
        var normalizedTarget = Normalize(target);
        if (string.IsNullOrWhiteSpace(normalizedTarget))
        {
            return false;
        }

        var normalizedProcess = Normalize(processName);
        var normalizedDisplay = Normalize(displayName);

        return IsLooseMatch(normalizedTarget, normalizedProcess) ||
               IsLooseMatch(normalizedTarget, normalizedDisplay);
    }

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
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        return WhitespaceRegex().Replace(builder.ToString(), " ").Trim();
    }

    private static bool IsLooseMatch(string target, string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        return candidate.Equals(target, StringComparison.OrdinalIgnoreCase) ||
               candidate.Contains(target, StringComparison.OrdinalIgnoreCase) ||
               target.Contains(candidate, StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
