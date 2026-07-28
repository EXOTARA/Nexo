namespace Nexo.App.DailyFlow;

/// <summary>
/// Diseño D3.1 — una entrada ya formateada para la lista de actividad reciente de Enfoque.
/// </summary>
public sealed record FocusHistorySessionSummary(
    string Label,
    string? TaskTitle,
    string DurationText,
    string TimestampText);

/// <summary>
/// Diseño D3.1 — resumen honesto del historial real de Enfoque: solo datos que
/// <see cref="Nexo.Core.Focus.FocusManager"/> ya calcula (nada de rachas, puntajes ni
/// comparativas inventadas).
/// </summary>
public sealed record FocusHistorySummary(
    IReadOnlyList<FocusHistorySessionSummary> RecentSessions,
    string? TopTaskLabel,
    int TopTaskMinutesToday)
{
    public bool HasRecentSessions => RecentSessions.Count > 0;

    public bool HasTopTask => !string.IsNullOrWhiteSpace(TopTaskLabel);
}
