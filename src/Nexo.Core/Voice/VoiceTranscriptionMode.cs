namespace Nexo.Core.Voice;

/// <summary>
/// Diseño D6.3 (Fase 3 — Kohana Flow) — para qué se va a usar la transcripción, porque eso cambia
/// cómo debe entregarse.
/// </summary>
public enum VoiceTranscriptionMode
{
    /// <summary>
    /// Comportamiento histórico y predeterminado: el texto pasa por
    /// <see cref="SpanishVoiceTranscriptNormalizer"/> para que el motor de comandos lo entienda
    /// (minúsculas, sin acentos, sin puntuación).
    /// </summary>
    Command,

    /// <summary>
    /// Dictado: se devuelve el texto de Whisper TAL CUAL, con sus acentos, mayúsculas y puntuación
    /// intactos. Aplicarle el normalizador de comandos destruiría justo lo que se quiere escribir.
    /// El acondicionamiento propio del dictado ocurre después, en
    /// <c>Nexo.Core.Flow.SpanishDictationNormalizer</c>.
    /// </summary>
    Dictation
}
