using Nexo.Core.Focus;

namespace Nexo.App.DailyFlow;

/// <summary>
/// Diseño D3.1 — función pura que transforma el estado real de <c>FocusManager</c> en un
/// <see cref="FocusDisplayState"/>. No conoce WPF ni ningún control; se prueba sin
/// <c>StaWpfFixture</c>, igual que <see cref="DailyFlowSummaryBuilder"/>.
/// </summary>
public static class FocusDisplayStateBuilder
{
    public static FocusDisplayState Build(
        FocusManager focusManager,
        DateTimeOffset now,
        Func<Guid, string?>? resolveTaskTitle = null)
    {
        ArgumentNullException.ThrowIfNull(focusManager);

        var snapshot = focusManager.GetSnapshot(now);
        var timer = snapshot.ActiveTimer;

        if (timer is null)
        {
            return new FocusDisplayState(
                HasSession: false,
                IsPaused: false,
                ClockText: "00:00",
                StatusText: "No hay una sesión de enfoque activa",
                AssociatedLabel: null,
                ProgressPercent: 0,
                CanPause: false,
                CanResume: false,
                CanFinish: false,
                CanStart: true,
                FocusMinutesToday: snapshot.FocusMinutesToday,
                CompletedSessionsToday: snapshot.CompletedSessionsToday);
        }

        var remaining = snapshot.Remaining;
        var elapsed = timer.Duration - remaining;
        var progress = timer.Duration.TotalSeconds <= 0
            ? 0
            : Math.Clamp(elapsed.TotalSeconds / timer.Duration.TotalSeconds * 100, 0, 100);
        var isPaused = timer.Status == FocusTimerStatus.Paused;

        // El título de la tarea se resuelve por Id en cada construcción (no se congela): si la
        // tarea ya no existe, o no hay ninguna asociada, se muestra la etiqueta de la sesión.
        var taskTitle = timer.TaskId.HasValue ? resolveTaskTitle?.Invoke(timer.TaskId.Value) : null;
        var associatedLabel = !string.IsNullOrWhiteSpace(taskTitle) ? taskTitle : timer.Label;

        return new FocusDisplayState(
            HasSession: true,
            IsPaused: isPaused,
            ClockText: FormatClock(remaining),
            StatusText: isPaused ? "En pausa" : "En curso",
            AssociatedLabel: associatedLabel,
            ProgressPercent: progress,
            CanPause: !isPaused,
            CanResume: isPaused,
            CanFinish: true,
            CanStart: false,
            FocusMinutesToday: snapshot.FocusMinutesToday,
            CompletedSessionsToday: snapshot.CompletedSessionsToday);
    }

    private static string FormatClock(TimeSpan remaining)
    {
        var totalHours = (int)remaining.TotalHours;
        return totalHours > 0
            ? $"{totalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}"
            : $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
    }
}
