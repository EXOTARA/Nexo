namespace Nexo.Core.Focus;

public sealed class FocusManager
{
    private static readonly TimeSpan MinimumDuration = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaximumDuration = TimeSpan.FromHours(24);

    private readonly object _sync = new();
    private readonly IFocusStore _store;
    private FocusState _state = new();

    public FocusManager(IFocusStore store)
    {
        _store = store;
    }

    public void Load()
    {
        lock (_sync)
        {
            _state = Normalize(_store.Load());
        }
    }

    public FocusDashboardSnapshot GetSnapshot(DateTimeOffset now)
    {
        lock (_sync)
        {
            var active = _state.ActiveTimer?.Copy();
            var remaining = active?.GetRemaining(now) ?? TimeSpan.Zero;
            var today = now.LocalDateTime.Date;
            var completedToday = _state.History
                .Where(entry => entry.CompletedAt.LocalDateTime.Date == today)
                .ToArray();
            var focused = completedToday
                .Where(entry => entry.Kind != FocusSessionKind.Break)
                .Sum(entry => entry.Duration.TotalMinutes);

            return new FocusDashboardSnapshot(
                active,
                remaining,
                completedToday.Length,
                (int)Math.Round(focused, MidpointRounding.AwayFromZero));
        }
    }

    public FocusOperationResult Start(
        TimeSpan duration,
        string? label,
        FocusSessionKind kind,
        DateTimeOffset now)
    {
        if (duration < MinimumDuration || duration > MaximumDuration)
        {
            return FocusOperationResult.Failed(
                "El temporizador debe durar entre 5 segundos y 24 horas.");
        }

        lock (_sync)
        {
            if (_state.ActiveTimer is not null)
            {
                return FocusOperationResult.Failed(
                    "Ya hay un temporizador activo. Termínalo o cancélalo antes de iniciar otro.");
            }

            var timer = new FocusTimer
            {
                Id = Guid.NewGuid(),
                Label = string.IsNullOrWhiteSpace(label)
                    ? GetDefaultLabel(kind)
                    : label.Trim(),
                Kind = kind,
                CreatedAt = now,
                StartedAt = now,
                EndsAt = now.Add(duration),
                Duration = duration,
                PausedRemaining = duration,
                Status = FocusTimerStatus.Running
            };

            _state.ActiveTimer = timer;
            SaveLocked();
            return FocusOperationResult.Completed(
                $"Inicié {timer.Label.ToLowerInvariant()} por {FormatDuration(duration)}.",
                timer.Copy());
        }
    }

    public FocusOperationResult Pause(DateTimeOffset now)
    {
        lock (_sync)
        {
            var timer = _state.ActiveTimer;
            if (timer is null)
            {
                return FocusOperationResult.Failed("No hay un temporizador activo.");
            }

            if (timer.Status == FocusTimerStatus.Paused)
            {
                return FocusOperationResult.Failed("El temporizador ya está en pausa.");
            }

            timer.PausedRemaining = timer.GetRemaining(now);
            timer.EndsAt = null;
            timer.Status = FocusTimerStatus.Paused;
            SaveLocked();
            return FocusOperationResult.Completed(
                $"Pausé {timer.Label.ToLowerInvariant()}.",
                timer.Copy());
        }
    }

    public FocusOperationResult Resume(DateTimeOffset now)
    {
        lock (_sync)
        {
            var timer = _state.ActiveTimer;
            if (timer is null)
            {
                return FocusOperationResult.Failed("No hay un temporizador activo.");
            }

            if (timer.Status == FocusTimerStatus.Running)
            {
                return FocusOperationResult.Failed("El temporizador ya está en curso.");
            }

            if (timer.PausedRemaining <= TimeSpan.Zero)
            {
                return FocusOperationResult.Failed("Ese temporizador ya no tiene tiempo restante.");
            }

            timer.EndsAt = now.Add(timer.PausedRemaining);
            timer.Status = FocusTimerStatus.Running;
            SaveLocked();
            return FocusOperationResult.Completed(
                $"Continué {timer.Label.ToLowerInvariant()}.",
                timer.Copy());
        }
    }

    public FocusOperationResult Cancel()
    {
        lock (_sync)
        {
            var timer = _state.ActiveTimer;
            if (timer is null)
            {
                return FocusOperationResult.Failed("No hay un temporizador activo.");
            }

            _state.ActiveTimer = null;
            SaveLocked();
            return FocusOperationResult.Completed(
                $"Cancelé {timer.Label.ToLowerInvariant()}.");
        }
    }

    public FocusCompletion? CollectCompletion(DateTimeOffset now)
    {
        lock (_sync)
        {
            var timer = _state.ActiveTimer;
            if (timer is null ||
                timer.Status != FocusTimerStatus.Running ||
                !timer.EndsAt.HasValue ||
                timer.EndsAt.Value > now)
            {
                return null;
            }

            var completedAt = timer.EndsAt.Value;
            var historyEntry = new FocusHistoryEntry
            {
                Id = timer.Id,
                Label = timer.Label,
                Kind = timer.Kind,
                StartedAt = timer.StartedAt,
                CompletedAt = completedAt,
                Duration = timer.Duration
            };

            _state.History.Add(historyEntry);
            _state.ActiveTimer = null;
            TrimHistoryLocked(now);
            SaveLocked();

            return new FocusCompletion(
                historyEntry.Label,
                historyEntry.Kind,
                historyEntry.Duration,
                historyEntry.CompletedAt);
        }
    }

    public string BuildStatus(DateTimeOffset now)
    {
        lock (_sync)
        {
            var timer = _state.ActiveTimer;
            if (timer is null)
            {
                return "No hay un temporizador activo.";
            }

            var remaining = timer.GetRemaining(now);
            var status = timer.Status == FocusTimerStatus.Paused
                ? "está en pausa"
                : "sigue en curso";

            return $"{timer.Label} {status}. Quedan {FormatRemaining(remaining)}.";
        }
    }

    private void SaveLocked() => _store.Save(_state.Copy());

    private void TrimHistoryLocked(DateTimeOffset now)
    {
        var cutoff = now.AddDays(-90);
        _state.History = _state.History
            .Where(entry => entry.CompletedAt >= cutoff)
            .OrderBy(entry => entry.CompletedAt)
            .TakeLast(500)
            .ToList();
    }

    private static FocusState Normalize(FocusState? state)
    {
        state ??= new FocusState();
        state.History ??= [];
        state.History = state.History
            .Where(entry => entry.Duration > TimeSpan.Zero)
            .Select(entry => entry.Copy())
            .ToList();

        if (state.ActiveTimer is { } timer)
        {
            if (timer.Duration <= TimeSpan.Zero ||
                timer.Duration > MaximumDuration ||
                string.IsNullOrWhiteSpace(timer.Label))
            {
                state.ActiveTimer = null;
            }
            else
            {
                timer.Label = timer.Label.Trim();
                timer.PausedRemaining = timer.PausedRemaining > TimeSpan.Zero
                    ? timer.PausedRemaining
                    : timer.Duration;
            }
        }

        return state;
    }

    private static string GetDefaultLabel(FocusSessionKind kind) => kind switch
    {
        FocusSessionKind.Focus => "Sesión de enfoque",
        FocusSessionKind.Study => "Sesión de estudio",
        FocusSessionKind.Break => "Descanso",
        _ => "Temporizador"
    };

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1 && duration.Minutes == 0)
        {
            var hours = (int)duration.TotalHours;
            return $"{hours} hora{(hours == 1 ? string.Empty : "s")}";
        }

        var minutes = Math.Max(1, (int)Math.Round(duration.TotalMinutes));
        return $"{minutes} minuto{(minutes == 1 ? string.Empty : "s")}";
    }

    private static string FormatRemaining(TimeSpan remaining)
    {
        if (remaining.TotalHours >= 1)
        {
            return $"{(int)remaining.TotalHours} h {remaining.Minutes:D2} min";
        }

        if (remaining.TotalMinutes >= 1)
        {
            return $"{(int)remaining.TotalMinutes} min {remaining.Seconds:D2} s";
        }

        return $"{Math.Max(0, remaining.Seconds)} segundos";
    }
}
