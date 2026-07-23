namespace Nexo.Core.Automation;

public sealed record RoutineCommand(
    RoutineCommandType Type,
    string OriginalText,
    string RoutineName = "",
    RoutineMatchConfidence Confidence = RoutineMatchConfidence.Explicit)
{
    public static RoutineCommand None(string originalText) =>
        new(RoutineCommandType.None, originalText);

    /// <summary>
    /// Una orden inferida es una hipótesis: comparte el verbo con otros dominios y solo debe
    /// ejecutarse si nadie más reclama la frase y la rutina existe de verdad.
    /// </summary>
    public bool IsInferred => Confidence == RoutineMatchConfidence.Inferred;
}
