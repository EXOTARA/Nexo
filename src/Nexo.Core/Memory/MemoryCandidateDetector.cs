using System.Text.RegularExpressions;

namespace Nexo.Core.Memory;

/// <summary>
/// Diseño D10 (Fase 6) — lee UNA frase y decide si contiene algo recordable. Es lo que D9 dejó
/// pendiente: hasta ahora la memoria solo se llenaba desde código.
///
/// Dos límites deliberados, los dos por el mismo motivo — el roadmap dice que la memoria no puede
/// convertirse en vigilancia permanente:
///
/// 1. **Solo detecta frases literales.** No infiere nada del resto de la conversación. Deducir que
///    alguien "parece preferir X" porque lo mencionó tres veces es justo la vigilancia silenciosa
///    que la fase prohíbe, y además sería adivinar.
/// 2. **Nunca produce <see cref="MemoryCategory.Habitos"/>.** Un hábito es una conducta observada a
///    lo largo del tiempo, no algo que se lea en una oración suelta. Sacar un "hábito" de una frase
///    sería inventarlo. Esa categoría existe y se respeta, pero solo puede llenarla algo que mida
///    conducta de verdad, y eso todavía no existe.
///
/// Que haya candidato NO significa que se guarde: <see cref="MemoryPolicy"/> sigue siendo la única
/// puerta, y un candidato observado además necesita un sí.
/// </summary>
public static partial class MemoryCandidateDetector
{
    /// <summary>Por debajo de esto no hay un hecho, hay una palabra suelta.</summary>
    private const int MinimumFactLength = 3;

    /// <summary>
    /// Por encima de esto es un párrafo, no un recuerdo. Guardar textos largos llenaría el tope de
    /// entradas con ruido y haría inútil la lista que la persona revisa en "ver lo que recuerda".
    /// </summary>
    private const int MaximumFactLength = 200;

    public static MemoryCandidate? Detect(string? text)
    {
        var normalized = CollapseWhitespace(text);
        if (normalized.Length == 0)
        {
            return null;
        }

        // Una pregunta no es una afirmación. "¿prefiero café o té?" no declara ninguna preferencia,
        // pregunta por ella.
        if (normalized.EndsWith('?') || normalized.StartsWith('¿'))
        {
            return null;
        }

        var explicitMatch = ExplicitRequestPattern().Match(normalized);
        if (explicitMatch.Success)
        {
            var fact = CleanFact(explicitMatch.Groups["fact"].Value);
            if (fact is null)
            {
                return null;
            }

            // La categoría la decide el contenido, no el disparador: "recuerda que prefiero
            // respuestas cortas" es una preferencia, aunque llegara por una orden explícita.
            var category = PreferenceStatementPattern().IsMatch(fact)
                ? MemoryCategory.Preferencias
                : MemoryCategory.Conversacion;

            return new MemoryCandidate(category, fact, MemoryCandidateSource.Explicito);
        }

        if (PreferenceStatementPattern().IsMatch(normalized))
        {
            var fact = CleanFact(normalized);
            return fact is null
                ? null
                : new MemoryCandidate(MemoryCategory.Preferencias, fact, MemoryCandidateSource.Observado);
        }

        return null;
    }

    /// <summary>
    /// Deja el hecho listo para guardarse: sin puntuación final suelta y dentro de los límites de
    /// longitud. Devuelve null cuando lo que queda no vale la pena recordar.
    /// </summary>
    private static string? CleanFact(string? fact)
    {
        var cleaned = CollapseWhitespace(fact).TrimEnd('.', ',', ';', ':', '!', ' ');
        if (cleaned.Length < MinimumFactLength || cleaned.Length > MaximumFactLength)
        {
            return null;
        }

        return cleaned;
    }

    private static string CollapseWhitespace(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : WhitespacePattern().Replace(text.Trim(), " ");

    /// <summary>
    /// Órdenes explícitas de recordar. **No** incluye "recuérdame" ni "apunta": ésas ya las reclama
    /// <c>SpanishTaskCommandParser</c> para crear tareas y recordatorios, y robárselas convertiría
    /// "recuérdame comprar pan" en un recuerdo en vez de en la tarea que la persona pidió.
    /// </summary>
    [GeneratedRegex(
        @"^(?:oye\s+(?:sakura|kohana)[,\s]+|(?:sakura|kohana)[,\s]+)?(?:recuerda\s+(?:que\s+|esto\s*[:,]\s*)|acu[eé]rdate\s+de\s+(?:que\s+)?|(?:quiero|necesito)\s+que\s+recuerdes\s+(?:que\s+)?|(?:toma\s+nota|ten\s+en\s+cuenta)\s+(?:de\s+)?que\s+)(?<fact>.+)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex ExplicitRequestPattern();

    /// <summary>
    /// Preferencias declaradas. Anclado al principio de la frase a propósito: "no sé si prefiero
    /// esto" contiene la palabra pero no declara nada.
    /// </summary>
    [GeneratedRegex(
        @"^(?:yo\s+)?(?:prefiero\s+|no\s+me\s+gusta[n]?\s+|me\s+gusta[n]?\s+m[aá]s\s+|mi\s+\w+\s+favorit[oa]\s+es\s+)",
        RegexOptions.IgnoreCase)]
    private static partial Regex PreferenceStatementPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
