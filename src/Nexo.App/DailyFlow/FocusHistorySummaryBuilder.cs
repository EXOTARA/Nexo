using Nexo.Core.Focus;

namespace Nexo.App.DailyFlow;

/// <summary>
/// Diseño D3.1 — traduce el historial real de <see cref="FocusManager"/> en un
/// <see cref="FocusHistorySummary"/> presentable. Lógica pura (sin WPF) para poder probarla sin
/// levantar ninguna vista, siguiendo el mismo patrón que <see cref="FocusDisplayStateBuilder"/>.
/// </summary>
public static class FocusHistorySummaryBuilder
{
    public const int MaxRecentSessions = 5;

    public static FocusHistorySummary Build(
        IReadOnlyList<FocusHistoryEntry> history,
        DateTimeOffset now,
        Func<Guid, string?>? resolveTaskTitle = null)
    {
        ArgumentNullException.ThrowIfNull(history);

        var recent = history
            .OrderByDescending(entry => entry.CompletedAt)
            .Take(MaxRecentSessions)
            .Select(entry => new FocusHistorySessionSummary(
                entry.Label,
                entry.TaskId is { } taskId ? resolveTaskTitle?.Invoke(taskId) : null,
                FormatDuration(entry.Duration),
                FormatTimestamp(entry.CompletedAt)))
            .ToList();

        var today = now.LocalDateTime.Date;
        var topTaskCandidate = history
            .Where(entry => entry.TaskId.HasValue && entry.CompletedAt.LocalDateTime.Date == today)
            .GroupBy(entry => entry.TaskId!.Value)
            .Select(group => new
            {
                TaskId = group.Key,
                Minutes = (int)Math.Round(
                    group.Sum(entry => entry.Duration.TotalMinutes),
                    MidpointRounding.AwayFromZero)
            })
            .Where(candidate => candidate.Minutes > 0)
            .OrderByDescending(candidate => candidate.Minutes)
            .FirstOrDefault();

        string? topTaskLabel = null;
        var topTaskMinutes = 0;
        if (topTaskCandidate is not null)
        {
            // Solo se muestra si la tarea sigue existiendo y tiene título: si fue borrada, no hay
            // nada honesto que anunciar como "la tarea con más tiempo hoy".
            var title = resolveTaskTitle?.Invoke(topTaskCandidate.TaskId);
            if (!string.IsNullOrWhiteSpace(title))
            {
                topTaskLabel = title;
                topTaskMinutes = topTaskCandidate.Minutes;
            }
        }

        return new FocusHistorySummary(recent, topTaskLabel, topTaskMinutes);
    }

    private static string FormatDuration(TimeSpan duration)
    {
        var totalMinutes = Math.Max(1, (int)Math.Round(duration.TotalMinutes, MidpointRounding.AwayFromZero));
        if (totalMinutes < 60)
        {
            return $"{totalMinutes} min";
        }

        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;
        return minutes == 0 ? $"{hours} h" : $"{hours} h {minutes:D2} min";
    }

    private static string FormatTimestamp(DateTimeOffset completedAt) =>
        $"{completedAt.LocalDateTime:ddd d MMM · HH:mm}";
}
