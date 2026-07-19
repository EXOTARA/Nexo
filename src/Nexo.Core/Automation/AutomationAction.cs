namespace Nexo.Core.Automation;

public sealed class AutomationAction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public AutomationActionType Type { get; set; }

    public bool IsEnabled { get; set; } = true;

    public string Target { get; set; } = string.Empty;

    public string Arguments { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public double? NumericValue { get; set; }

    public AutomationAction Copy() => new()
    {
        Id = Id,
        Type = Type,
        IsEnabled = IsEnabled,
        Target = Target,
        Arguments = Arguments,
        WorkingDirectory = WorkingDirectory,
        Text = Text,
        NumericValue = NumericValue
    };
}
