namespace Nexo.Core.Flow;

/// <summary>
/// Diseño D6.1 — una sustitución del diccionario personal: cuando el dictado produce
/// <see cref="Spoken"/> como palabra completa, se reemplaza por <see cref="Replacement"/>. Pensado
/// para nombres propios y términos técnicos que Whisper escribe mal ("cojana" → "Sakura").
/// </summary>
public sealed record FlowDictionaryEntry(string Spoken, string Replacement);

/// <summary>
/// Diseño D6.1 — un atajo: al dictar <see cref="Trigger"/> se inserta <see cref="Expansion"/> tal
/// cual. A diferencia del diccionario, la expansión se inserta verbatim y ya no se vuelve a
/// procesar (así un atajo puede contener puntuación real sin que se reinterprete).
/// </summary>
public sealed record FlowSnippet(string Trigger, string Expansion);

/// <summary>Diseño D6.1 — todo lo que <see cref="SpanishDictationNormalizer"/> necesita saber.</summary>
public sealed record FlowDictationOptions(
    FlowMode Mode = FlowMode.Texto,
    IReadOnlyList<FlowDictionaryEntry>? Dictionary = null,
    IReadOnlyList<FlowSnippet>? Snippets = null,
    bool RemoveFillers = true)
{
    public static readonly FlowDictationOptions Default = new();
}
