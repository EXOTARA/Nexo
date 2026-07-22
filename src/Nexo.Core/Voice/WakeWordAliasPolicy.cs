namespace Nexo.Core.Voice;

public static class WakeWordAliasPolicy
{
    public const int MaximumAliases = 8;

    public static bool TryNormalize(
        string? value,
        out string normalized,
        out string detail)
    {
        normalized = WakeWordTextMatcher.Normalize(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            detail = "No hay una frase reconocida para guardar.";
            return false;
        }

        if (normalized.Length < 3 || normalized.Length > 36)
        {
            detail = "El alias debe tener entre 3 y 36 caracteres.";
            return false;
        }

        var words = normalized.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length > 4)
        {
            detail = "El alias debe ser una frase corta de hasta cuatro palabras.";
            return false;
        }

        if (words.Any(word => word is "nexo" or "neso"))
        {
            detail = "Los aliases heredados de Nexo no se agregan al modo Kohana.";
            return false;
        }

        detail = "Alias válido.";
        return true;
    }

    public static List<string> NormalizeMany(IEnumerable<string>? aliases)
    {
        if (aliases is null)
        {
            return [];
        }

        var result = new List<string>();
        foreach (var alias in aliases)
        {
            if (!TryNormalize(alias, out var normalized, out _) ||
                result.Contains(normalized, StringComparer.Ordinal))
            {
                continue;
            }

            result.Add(normalized);
            if (result.Count >= MaximumAliases)
            {
                break;
            }
        }

        return result;
    }
}
