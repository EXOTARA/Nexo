namespace Nexo.Core.Flow;

/// <summary>
/// Diseño D6.3 — traduce las líneas "dicho=escrito" que guarda <c>ShellPreferences</c> a las
/// entradas que entiende <see cref="SpanishDictationNormalizer"/>. Tolera líneas mal escritas en
/// silencio: el archivo de ajustes puede editarse a mano, y una línea inválida no debe impedir que
/// el dictado funcione con las demás.
/// </summary>
public static class FlowSettingsParser
{
    public static IReadOnlyList<FlowDictionaryEntry> ParseDictionary(IEnumerable<string>? lines) =>
        ParsePairs(lines)
            .Select(pair => new FlowDictionaryEntry(pair.Spoken, pair.Written))
            .ToArray();

    public static IReadOnlyList<FlowSnippet> ParseSnippets(IEnumerable<string>? lines) =>
        ParsePairs(lines)
            .Select(pair => new FlowSnippet(pair.Spoken, pair.Written))
            .ToArray();

    private static IEnumerable<(string Spoken, string Written)> ParsePairs(IEnumerable<string>? lines)
    {
        if (lines is null)
        {
            yield break;
        }

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // Solo el PRIMER "=" separa: así una expansión puede contener signos de igual
            // ("total = suma") sin que la línea se rompa.
            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var spoken = line[..separator].Trim();
            var written = line[(separator + 1)..].Trim();

            if (spoken.Length > 0 && written.Length > 0)
            {
                yield return (spoken, written);
            }
        }
    }
}
