namespace Nexo.Core.Automation;

public sealed record RoutineExecutionReport(
    RoutineDefinition Routine,
    IReadOnlyList<AutomationActionResult> Results,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt)
{
    public int SucceededCount => Results.Count(result => result.Success);

    public int FailedCount => Results.Count - SucceededCount;

    public bool Succeeded => FailedCount == 0;

    public string BuildSummary()
    {
        var lines = new List<string>
        {
            $"Rutina {Routine.Name}: {SucceededCount} de {Results.Count} acciones completadas."
        };

        lines.AddRange(Results.Select(result =>
            $"{(result.Success ? "✓" : "⚠")} {result.Detail}"));
        return string.Join(Environment.NewLine, lines);
    }
}
