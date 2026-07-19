namespace Nexo.Core.Focus;

public sealed class FocusHistoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Label { get; set; } = string.Empty;

    public FocusSessionKind Kind { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset CompletedAt { get; set; }

    public TimeSpan Duration { get; set; }

    public FocusHistoryEntry Copy() => new()
    {
        Id = Id,
        Label = Label,
        Kind = Kind,
        StartedAt = StartedAt,
        CompletedAt = CompletedAt,
        Duration = Duration
    };
}
