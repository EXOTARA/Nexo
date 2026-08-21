using System.Text;
using System.Text.RegularExpressions;

namespace Nexo.Core.Flow;

/// <summary>
/// Diseño D6.1 (Fase 3 — Sakura Flow) — convierte la transcripción CRUDA de Whisper en texto listo
/// para insertar en otra aplicación.
///
/// **No confundir con <see cref="Nexo.Core.Voice.SpanishVoiceTranscriptNormalizer"/>**: aquel
/// normaliza para el motor de COMANDOS y es deliberadamente destructivo (pasa todo a minúsculas,
/// quita acentos y sustituye cada signo de puntuación por un espacio). Eso es correcto para
/// entender "abre powershell" y catastrófico para dictar un correo. Por eso Flow necesita su propia
/// ruta de transcripción sin normalización de comandos (ver <c>VoiceTranscriptionMode</c>) y este
/// normalizador aparte, que preserva acentos, mayúsculas y puntuación reales.
///
/// Orden de operaciones (importa, y es estable): 1) espacios, 2) muletillas, 3) puntuación y
/// símbolos hablados, 4) diccionario personal, 5) atajos (expansión verbatim), 6) espaciado
/// alrededor de la puntuación, 7) mayúscula de oración — omitida en <see cref="FlowMode.Codigo"/>.
/// </summary>
public static partial class SpanishDictationNormalizer
{
    /// <summary>
    /// Solo sonidos de duda sin significado propio en español. Deliberadamente NO incluye "este",
    /// "o sea", "bueno", "pues" ni "digamos": aunque se usan como muletillas, también son palabras
    /// reales ("este archivo", "bueno para ti"). Borrar por error una palabra que la persona sí
    /// quiso decir es peor que dejar una muletilla — quien dicta no siempre relee antes de enviar.
    /// </summary>
    private static readonly string[] Fillers =
    [
        "eh", "ehh", "ehhh", "eeh", "eee", "em", "emm", "ehm", "mm", "mmm", "aam", "ajam"
    ];

    /// <summary>Sin espacio ANTES: cierran o se pegan a la palabra anterior.</summary>
    private const string NoSpaceBefore = ".,;:?!)]}…»";

    /// <summary>Sin espacio DESPUÉS: abren y se pegan a la palabra siguiente.</summary>
    private const string NoSpaceAfter = "([{¿¡«";

    /// <summary>
    /// Sin espacio a NINGÚN lado: forman parte de una sola palabra (correo, identificadores).
    /// Deliberadamente NO incluye el guion: Whisper también escribe guiones por su cuenta como
    /// inciso en prosa ("esto - aquello"), y pegarlos convertiría en "esto-aquello" algo que la
    /// persona sí dictó bien. Prefiero dejar "kebab - case" mal separado (raro, y visible de
    /// inmediato) antes que alterar texto que ya estaba correcto.
    /// </summary>
    private const string NoSpaceEitherSide = "@_";

    /// <summary>
    /// Puntuación común a los tres modos. Orden = prioridad: las frases largas van primero para que
    /// "punto y aparte" no se resuelva antes como "punto" seguido de "y aparte".
    /// </summary>
    private static readonly (string Spoken, string Replacement)[] CommonPunctuation =
    [
        ("punto y aparte", "\n\n"),
        ("punto y seguido", "."),
        ("punto y coma", ";"),
        ("puntos suspensivos", "…"),
        ("dos puntos", ":"),
        ("nuevo parrafo", "\n\n"),
        ("nueva linea", "\n"),
        ("salto de linea", "\n"),
        ("abre interrogacion", "¿"),
        ("cierra interrogacion", "?"),
        ("signo de interrogacion", "?"),
        ("abre exclamacion", "¡"),
        ("cierra exclamacion", "!"),
        ("signo de exclamacion", "!"),
        ("abre comillas", "\""),
        ("cierra comillas", "\""),
        ("abre parentesis", "("),
        ("cierra parentesis", ")"),
        ("guion bajo", "_"),
        ("arroba", "@"),
        ("punto", "."),
        ("coma", ","),
        ("guion", "-")
    ];

    /// <summary>Símbolos que solo tienen sentido dictando código.</summary>
    private static readonly (string Spoken, string Replacement)[] CodePunctuation =
    [
        ("abre llave", "{"),
        ("cierra llave", "}"),
        ("abre corchete", "["),
        ("cierra corchete", "]"),
        ("menor que", "<"),
        ("mayor que", ">"),
        ("flecha", "=>"),
        ("asterisco", "*"),
        ("barra", "/"),
        ("igual", "=")
    ];

    public static string Normalize(string? rawTranscript, FlowDictationOptions? options = null)
    {
        options ??= FlowDictationOptions.Default;

        if (string.IsNullOrWhiteSpace(rawTranscript))
        {
            return string.Empty;
        }

        var text = WhitespaceRegex().Replace(rawTranscript.Trim(), " ");

        if (options.RemoveFillers)
        {
            text = RemoveFillers(text);
        }

        text = ApplySpokenSymbols(text, options.Mode);
        text = ApplyDictionary(text, options.Dictionary);
        text = ApplySnippets(text, options.Snippets);
        text = FixSpacing(text);

        if (options.Mode != FlowMode.Codigo)
        {
            text = CapitalizeSentences(text);
        }

        return text.Trim();
    }

    private static string RemoveFillers(string text)
    {
        foreach (var filler in Fillers)
        {
            text = Regex.Replace(
                text,
                $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(filler)}(?![\p{{L}}\p{{N}}])[,]?",
                " ",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return WhitespaceRegex().Replace(text, " ").Trim();
    }

    private static string ApplySpokenSymbols(string text, FlowMode mode)
    {
        var table = mode == FlowMode.Codigo
            ? CodePunctuation.Concat(CommonPunctuation)
            : CommonPunctuation;

        foreach (var (spoken, replacement) in table)
        {
            text = Regex.Replace(
                text,
                $@"(?<![\p{{L}}\p{{N}}]){BuildAccentFlexiblePattern(spoken)}(?![\p{{L}}\p{{N}}])",
                replacement.Replace("$", "$$"),
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return text;
    }

    /// <summary>
    /// Whisper puede escribir "interrogación" o "interrogacion" según el audio; el patrón acepta
    /// ambas sin que haya que listar cada variante a mano.
    /// </summary>
    private static string BuildAccentFlexiblePattern(string phrase)
    {
        var builder = new StringBuilder(phrase.Length * 4);
        foreach (var character in phrase)
        {
            builder.Append(character switch
            {
                'a' => "[aá]",
                'e' => "[eé]",
                'i' => "[ií]",
                'o' => "[oó]",
                'u' => "[uúü]",
                'n' => "[nñ]",
                ' ' => @"\s+",
                _ => Regex.Escape(character.ToString())
            });
        }

        return builder.ToString();
    }

    private static string ApplyDictionary(string text, IReadOnlyList<FlowDictionaryEntry>? dictionary)
    {
        if (dictionary is null)
        {
            return text;
        }

        foreach (var entry in dictionary)
        {
            if (string.IsNullOrWhiteSpace(entry.Spoken))
            {
                continue;
            }

            text = Regex.Replace(
                text,
                $@"(?<![\p{{L}}\p{{N}}]){BuildAccentFlexiblePattern(entry.Spoken.Trim().ToLowerInvariant())}(?![\p{{L}}\p{{N}}])",
                (entry.Replacement ?? string.Empty).Replace("$", "$$"),
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return text;
    }

    private static string ApplySnippets(string text, IReadOnlyList<FlowSnippet>? snippets)
    {
        if (snippets is null)
        {
            return text;
        }

        foreach (var snippet in snippets)
        {
            if (string.IsNullOrWhiteSpace(snippet.Trigger))
            {
                continue;
            }

            text = Regex.Replace(
                text,
                $@"(?<![\p{{L}}\p{{N}}]){BuildAccentFlexiblePattern(snippet.Trigger.Trim().ToLowerInvariant())}(?![\p{{L}}\p{{N}}])",
                (snippet.Expansion ?? string.Empty).Replace("$", "$$"),
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return text;
    }

    private static string FixSpacing(string text)
    {
        var builder = new StringBuilder(text.Length);

        foreach (var character in text)
        {
            if (character == ' ' && builder.Length > 0)
            {
                var previous = builder[^1];
                if (NoSpaceAfter.Contains(previous) || NoSpaceEitherSide.Contains(previous))
                {
                    continue;
                }
            }

            if ((NoSpaceBefore.Contains(character) || NoSpaceEitherSide.Contains(character)) &&
                builder.Length > 0 && builder[^1] == ' ')
            {
                builder.Length--;
            }

            builder.Append(character);
        }

        // Los saltos de línea quedan con espacios colgando a los lados tras las sustituciones.
        var result = Regex.Replace(builder.ToString(), @"[ \t]*\n[ \t]*", "\n");
        result = Regex.Replace(result, "[ \t]{2,}", " ");
        return RepairDictatedDomains(result);
    }

    /// <summary>
    /// Dominios de primer nivel frecuentes en México y en general. La lista es corta a propósito:
    /// ver <see cref="RepairDictatedDomains"/>.
    /// </summary>
    private static readonly string[] TopLevelDomains =
    [
        "com", "mx", "org", "net", "edu", "gob", "io", "dev", "es", "co", "info", "app", "me", "tv"
    ];

    /// <summary>
    /// Dictar "adler arroba gmail punto com" produce "adler@gmail. com": el punto es correcto, pero
    /// el espacio que le sigue —regla de prosa— parte la dirección en dos. Este paso vuelve a unirla.
    ///
    /// El punto de un dominio y el de fin de oración son idénticos como carácter, así que la única
    /// señal fiable es lo que viene DESPUÉS. Por eso se exige un dominio de primer nivel conocido y
    /// no simplemente "una palabra corta": así "adler@ejemplo.com. luego te escribo" conserva su
    /// punto final y no se convierte en "…com.luego". Un TLD fuera de la lista queda separado —
    /// visible y corregible a mano— en vez de pegar por error dos oraciones distintas.
    /// </summary>
    private static string RepairDictatedDomains(string text)
    {
        var pattern =
            $@"(\S*@\S*)\.\s+(?=(?:{string.Join('|', TopLevelDomains)})(?![\p{{L}}\p{{N}}]))";

        // Se repite hasta estabilizar porque Regex.Replace reanuda DESPUÉS de cada coincidencia:
        // en "…ejemplo. com. mx" la primera pasada une "ejemplo.com" y deja el motor situado más
        // allá del segundo punto, que solo se ve en la vuelta siguiente. El tope evita cualquier
        // posibilidad de bucle infinito si el patrón cambiara en el futuro.
        var current = text;
        for (var iteration = 0; iteration < 8; iteration++)
        {
            var next = Regex.Replace(
                current, pattern, "$1.", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (next == current)
            {
                break;
            }

            current = next;
        }

        return current;
    }

    private static string CapitalizeSentences(string text)
    {
        var builder = new StringBuilder(text);
        var capitalizeNext = true;

        for (var i = 0; i < builder.Length; i++)
        {
            var character = builder[i];

            if (capitalizeNext && char.IsLetter(character))
            {
                // Una dirección de correo no lleva mayúscula inicial aunque abra la oración:
                // "Adler@gmail.com" está mal escrito y es justo lo que produce dictar un correo
                // en modo Correo, que es donde más se espera que esto salga bien.
                if (!IsInsideEmailLikeToken(builder, i))
                {
                    builder[i] = char.ToUpperInvariant(character);
                }

                capitalizeNext = false;
                continue;
            }

            if (character is '.' or '?' or '!' or '\n')
            {
                capitalizeNext = true;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Examina la palabra COMPLETA a la que pertenece <paramref name="index"/>, hacia atrás y hacia
    /// adelante. Mirar solo hacia adelante no basta: en "adler@gmail.com" el punto de "gmail."
    /// dispara la mayúscula de la siguiente oración justo antes de "com", donde la arroba ya quedó
    /// detrás — y el resultado era "adler@gmail. Com".
    /// </summary>
    private static bool IsInsideEmailLikeToken(StringBuilder builder, int index)
    {
        for (var i = index; i >= 0 && !char.IsWhiteSpace(builder[i]); i--)
        {
            if (builder[i] == '@')
            {
                return true;
            }
        }

        for (var i = index; i < builder.Length && !char.IsWhiteSpace(builder[i]); i++)
        {
            if (builder[i] == '@')
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
