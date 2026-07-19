namespace Nexo.Core.Automation;

public sealed record RoutineOperationResult(
    bool Success,
    string Message,
    RoutineDefinition? Routine = null)
{
    public static RoutineOperationResult Completed(
        string message,
        RoutineDefinition? routine = null) =>
        new(true, message, routine?.Copy());

    public static RoutineOperationResult Failed(string message) =>
        new(false, message);
}
