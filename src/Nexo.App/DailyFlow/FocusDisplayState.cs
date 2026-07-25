namespace Nexo.App.DailyFlow;

/// <summary>
/// Diseño D3.1 — instantánea inmutable de "qué debe mostrarse y qué acciones tienen sentido ahora"
/// para el estado de Enfoque. La construye <see cref="FocusDisplayStateBuilder"/> a partir de
/// <c>FocusManager</c>; la consumen el mini temporizador global, la tarjeta de Enfoque de Inicio y
/// (indirectamente) <c>FocusView</c>, para no repetir el mismo cálculo en tres lugares distintos.
/// </summary>
public sealed record FocusDisplayState(
    bool HasSession,
    bool IsPaused,
    string ClockText,
    string StatusText,
    string? AssociatedLabel,
    double ProgressPercent,
    bool CanPause,
    bool CanResume,
    bool CanFinish,
    bool CanStart,
    int FocusMinutesToday,
    int CompletedSessionsToday)
{
    /// <summary>El mini temporizador (y cualquier otro indicador compacto) solo se muestra con sesión.</summary>
    public bool IsVisible => HasSession;
}
