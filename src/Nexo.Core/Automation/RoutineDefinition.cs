namespace Nexo.Core.Automation;

public sealed class RoutineDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string TriggerPhrase { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public bool RequiresConfirmation { get; set; } = true;

    public List<AutomationAction> Steps { get; set; } = [];

    public RoutineDefinition Copy() => new()
    {
        Id = Id,
        Name = Name,
        TriggerPhrase = TriggerPhrase,
        IsEnabled = IsEnabled,
        RequiresConfirmation = RequiresConfirmation,
        Steps = Steps.Select(step => step.Copy()).ToList()
    };
}
