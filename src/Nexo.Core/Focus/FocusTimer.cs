namespace Nexo.Core.Focus;

public sealed class FocusTimer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Label { get; set; } = "Temporizador";

    public FocusSessionKind Kind { get; set; } = FocusSessionKind.Custom;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset? EndsAt { get; set; }

    public TimeSpan Duration { get; set; }

    public TimeSpan PausedRemaining { get; set; }

    public FocusTimerStatus Status { get; set; } = FocusTimerStatus.Running;

    public TimeSpan GetRemaining(DateTimeOffset now)
    {
        if (Status == FocusTimerStatus.Paused)
        {
            return PausedRemaining > TimeSpan.Zero
                ? PausedRemaining
                : TimeSpan.Zero;
        }

        if (!EndsAt.HasValue)
        {
            return TimeSpan.Zero;
        }

        var remaining = EndsAt.Value - now;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    public FocusTimer Copy() => new()
    {
        Id = Id,
        Label = Label,
        Kind = Kind,
        CreatedAt = CreatedAt,
        StartedAt = StartedAt,
        EndsAt = EndsAt,
        Duration = Duration,
        PausedRemaining = PausedRemaining,
        Status = Status
    };
}
