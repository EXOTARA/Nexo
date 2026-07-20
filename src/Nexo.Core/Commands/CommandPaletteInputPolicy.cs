namespace Nexo.Core.Commands;

public static class CommandPaletteInputPolicy
{
    public static bool IsLikelyNaturalPrompt(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        var words = trimmed.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return trimmed.Length >= 28 ||
               words.Length >= 5 ||
               trimmed.IndexOfAny(new[] { '?', '¿', '.', '!', '¡', ':', ';', ',' }) >= 0;
    }

    public static bool IsExactSuggestionMatch(
        string? query,
        string title,
        string command,
        IEnumerable<string> keywords)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        var normalized = Normalize(query);
        return Normalize(title) == normalized ||
               Normalize(command) == normalized ||
               keywords.Any(keyword => Normalize(keyword) == normalized);
    }

    public static bool ShouldExecuteSuggestion(
        string? query,
        string title,
        string command,
        IEnumerable<string> keywords,
        int score,
        bool completionActive,
        bool selectionExplicit)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        if (completionActive || selectionExplicit)
        {
            return true;
        }

        if (IsExactSuggestionMatch(query, title, command, keywords))
        {
            return true;
        }

        if (IsLikelyNaturalPrompt(query))
        {
            return false;
        }

        var trimmed = query.Trim();
        return trimmed.Length <= 14 && score >= 220;
    }

    private static string Normalize(string value) =>
        string.Join(
            ' ',
            value.Trim()
                .ToLowerInvariant()
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries));
}
