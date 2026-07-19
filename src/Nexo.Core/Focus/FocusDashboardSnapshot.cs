namespace Nexo.Core.Focus;

public sealed record FocusDashboardSnapshot(
    FocusTimer? ActiveTimer,
    TimeSpan Remaining,
    int CompletedSessionsToday,
    int FocusMinutesToday);
