namespace Nexo.Core.Ai;

public static class OllamaModelName
{
    public static string? Normalize(string? model)
    {
        var value = (model ?? string.Empty).Trim();
        if (value.Length is < 1 or > 120)
        {
            return null;
        }

        return value.All(character =>
                char.IsLetterOrDigit(character) ||
                character is '.' or ':' or '-' or '_' or '/')
            ? value
            : null;
    }
}
