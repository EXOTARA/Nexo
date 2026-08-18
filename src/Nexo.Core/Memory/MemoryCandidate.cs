namespace Nexo.Core.Memory;

/// <summary>
/// Diseño D10 (Fase 6) — de dónde salió un candidato a recuerdo. La distinción no es cosmética:
/// decide si Kohana puede guardarlo tras pasar por <see cref="MemoryPolicy"/> o si además tiene que
/// preguntar.
/// </summary>
public enum MemoryCandidateSource
{
    /// <summary>
    /// La persona pidió explícitamente que se recordara ("recuerda que ..."). Guardar es
    /// obedecer la orden que acaba de dar.
    /// </summary>
    Explicito,

    /// <summary>
    /// Una preferencia dicha de paso ("prefiero respuestas cortas"). Nadie pidió que se guardara,
    /// así que **nunca** se guarda sola: se propone y hace falta un sí.
    /// </summary>
    Observado
}

/// <summary>
/// Diseño D10 — algo que PODRÍA recordarse. Que exista un candidato no significa que se guarde:
/// sigue teniendo que pasar por <see cref="MemoryPolicy"/>, que es la única puerta.
/// </summary>
public sealed record MemoryCandidate(
    MemoryCategory Category,
    string Text,
    MemoryCandidateSource Source);
