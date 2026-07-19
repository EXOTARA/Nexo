namespace Nexo.Core.Automation;

public sealed record RoutineCommand(
    RoutineCommandType Type,
    string OriginalText,
    string RoutineName = "")
{
    public static RoutineCommand None(string originalText) =>
        new(RoutineCommandType.None, originalText);
}
