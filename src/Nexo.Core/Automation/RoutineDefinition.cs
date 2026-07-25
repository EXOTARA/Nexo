namespace Nexo.Core.Automation;

public sealed class RoutineDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string TriggerPhrase { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public bool RequiresConfirmation { get; set; } = true;

    public List<AutomationAction> Steps { get; set; } = [];

    /// <summary>Diseño D3: cuándo se ejecutó por última vez. Anulable — nunca ejecutada todavía.</summary>
    public DateTimeOffset? LastExecutedAt { get; set; }

    /// <summary>
    /// Diseño D3: si la última ejecución completó sin ninguna acción fallida
    /// (<see cref="RoutineExecutionReport.Succeeded"/>). Anulable: sin significado hasta la
    /// primera ejecución.
    /// </summary>
    public bool? LastExecutionSucceeded { get; set; }

    public RoutineDefinition Copy() => new()
    {
        Id = Id,
        Name = Name,
        TriggerPhrase = TriggerPhrase,
        IsEnabled = IsEnabled,
        RequiresConfirmation = RequiresConfirmation,
        Steps = Steps.Select(step => step.Copy()).ToList(),
        LastExecutedAt = LastExecutedAt,
        LastExecutionSucceeded = LastExecutionSucceeded
    };
}
