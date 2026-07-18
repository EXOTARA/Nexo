using System.Text.RegularExpressions;

namespace Nexo.Core.Commands;

/// <summary>
/// Pequeño diccionario controlado para aceptar variaciones gramaticales y
/// errores cercanos frecuentes sin convertir cualquier frase en un comando.
/// Solo corrige la primera palabra cuando la frase parece una orden corta.
/// </summary>
public static partial class SpanishCommandLexicon
{
    private static readonly IReadOnlyDictionary<string, string> LeadingAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["bajas"] = "baja",
            ["bajale"] = "baja",
            ["bajame"] = "baja",
            ["bajar"] = "baja",
            ["reduces"] = "reduce",
            ["reducir"] = "reduce",
            ["disminuye"] = "baja",
            ["disminuyes"] = "baja",
            ["disminuir"] = "baja",

            ["subes"] = "sube",
            ["subele"] = "sube",
            ["subeme"] = "sube",
            ["subir"] = "sube",
            ["aumentas"] = "aumenta",
            ["aumentar"] = "aumenta",

            ["ponle"] = "pon",
            ["ponlo"] = "pon",
            ["ponla"] = "pon",
            ["ajustalo"] = "ajusta",
            ["ajustala"] = "ajusta",
            ["dejalo"] = "deja",
            ["dejala"] = "deja",

            ["silencias"] = "silencia",
            ["silenciar"] = "silencia",
            ["muteas"] = "mutea",
            ["mutear"] = "mutea",

            ["abreme"] = "abre",
            ["abrelo"] = "abre",
            ["abrela"] = "abre",
            ["muestrame"] = "muestra",
            ["ensena"] = "muestra",
            ["ensename"] = "muestra",
            ["revisame"] = "revisa"
        };

    private static readonly IReadOnlyDictionary<string, string> KnownWordAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["potify"] = "spotify",
            ["espotify"] = "spotify",
            ["spotifai"] = "spotify",
            ["spotifay"] = "spotify",
            ["spoty"] = "spotify",
            ["spoti"] = "spotify",
            ["discor"] = "discord",
            ["powershield"] = "powershell",
            ["powersheld"] = "powershell",
            ["powershel"] = "powershell"
        };

    private static readonly string[] CanonicalLeadingWords =
    [
        "baja", "reduce", "sube", "aumenta", "pon", "ajusta", "establece",
        "deja", "silencia", "mutea", "abre", "muestra", "revisa"
    ];

    private static readonly HashSet<string> CommandAnchors =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "volumen", "sonido", "audio", "spotify", "discord", "zen",
            "firefox", "chrome", "edge", "peek", "powershell", "terminal",
            "cmd", "pc", "computadora", "equipo", "sistema"
        };

    public static string NormalizeForParsing(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = WhitespaceRegex().Replace(text.Trim(), " ");
        normalized = CourtesyPrefixRegex().Replace(normalized, string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (var index = 0; index < words.Length; index++)
        {
            if (KnownWordAliases.TryGetValue(words[index], out var canonicalWord))
            {
                words[index] = canonicalWord;
            }
        }

        if (LeadingAliases.TryGetValue(words[0], out var canonicalLead))
        {
            words[0] = canonicalLead;
        }
        else if (ShouldTryNearLeadingMatch(words) &&
                 TryFindUniqueNearLeadingWord(words[0], out var nearLead))
        {
            words[0] = nearLead;
        }

        return string.Join(' ', words);
    }

    public static string NormalizeTarget(string? target)
    {
        var normalized = target?.Trim() ?? string.Empty;
        return KnownWordAliases.TryGetValue(normalized, out var canonical)
            ? canonical
            : normalized;
    }

    private static bool ShouldTryNearLeadingMatch(string[] words)
    {
        if (words.Length is < 2 or > 10 || words[0].Length < 4)
        {
            return false;
        }

        return words.Skip(1).Any(word =>
            CommandAnchors.Contains(word) ||
            word.Any(char.IsDigit));
    }

    private static bool TryFindUniqueNearLeadingWord(string word, out string canonical)
    {
        canonical = string.Empty;
        var bestDistance = int.MaxValue;
        string? bestCandidate = null;
        var hasTie = false;

        foreach (var candidate in CanonicalLeadingWords)
        {
            var distance = LevenshteinDistance(word, candidate);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestCandidate = candidate;
                hasTie = false;
            }
            else if (distance == bestDistance)
            {
                hasTie = true;
            }
        }

        if (bestDistance != 1 || hasTie || bestCandidate is null)
        {
            return false;
        }

        canonical = bestCandidate;
        return true;
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var index = 0; index <= right.Length; index++)
        {
            previous[index] = index;
        }

        for (var leftIndex = 1; leftIndex <= left.Length; leftIndex++)
        {
            current[0] = leftIndex;

            for (var rightIndex = 1; rightIndex <= right.Length; rightIndex++)
            {
                var substitutionCost = left[leftIndex - 1] == right[rightIndex - 1] ? 0 : 1;
                current[rightIndex] = Math.Min(
                    Math.Min(current[rightIndex - 1] + 1, previous[rightIndex] + 1),
                    previous[rightIndex - 1] + substitutionCost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    [GeneratedRegex(@"^(?:(?:por\s+favor)|oye|hey)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex CourtesyPrefixRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
