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

    /// <summary>
    /// Diseño D3: tarea asociada a esta sesión, cuando se inició desde "Enfocarme" en Hoy.
    /// Anulable y opcional a propósito — la mayoría de sesiones no tienen una tarea asociada, y un
    /// archivo de enfoque anterior a D3 simplemente lo deserializa como <c>null</c>. Es solo
    /// informativo: si la tarea se edita o elimina después, la sesión sigue siendo válida.
    /// </summary>
    public Guid? TaskId { get; set; }

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
        Status = Status,
        TaskId = TaskId
    };
}
