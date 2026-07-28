using System.Globalization;
using Nexo.Core.Automation;
using Nexo.Core.Focus;
using Nexo.Core.Tasks;
using Nexo.App.Views;

namespace Nexo.App.DailyFlow;

/// <summary>
/// Diseño D3 — construye el resumen de Inicio a partir de los managers reales. Extraído de
/// <c>MainWindow.RefreshHomeView()</c> (antes ~70 líneas de lógica de dominio en code-behind) para
/// que el cálculo del resumen sea una función pura y comprobable sin WPF: mismos datos de entrada,
/// mismo resultado, sin depender de ningún control ni de <c>MainWindow</c>.
///
/// No inventa datos: cuando no hay tareas, sesión o rutinas, produce un texto de estado vacío
/// honesto en vez de rellenar con un valor de relleno (el "25 min" fijo que mostraba la tarjeta de
/// Enfoque antes de D3, incluso sin ninguna sesión activa, se corrige aquí).
/// </summary>
public static class DailyFlowSummaryBuilder
{
    public static HomeDashboardViewModel BuildHomeDashboard(
        TaskManager taskManager,
        FocusManager focusManager,
        IReadOnlyList<RoutineDefinition> routines,
        bool hasRememberedExternalWindow,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(taskManager);
        ArgumentNullException.ThrowIfNull(focusManager);
        ArgumentNullException.ThrowIfNull(routines);

        var localNow = now.LocalDateTime;
        var culture = new CultureInfo("es-MX");

        // Corrección (2026-07-28): el saludo debe depender de la hora que trae `now` (su propio
        // Offset), no de convertirla a la zona horaria de la máquina que ejecuta el código. Usar
        // `localNow.Hour` reinterpretaba el DateTimeOffset de entrada en la zona horaria local del
        // proceso, así que la misma prueba con hora fija (p. ej. 07:00 con offset -6) daba un
        // saludo distinto según el huso horario del equipo — es justo lo que rompía la ejecución
        // en GitHub Actions (runners en UTC) mientras pasaba en un equipo en zona -6. `now.Hour`
        // es la hora ya fijada en el propio valor, sin reconversión.
        var greeting = now.Hour switch
        {
            < 6 => "Buenas noches",
            < 12 => "Buenos días",
            < 19 => "Buenas tardes",
            _ => "Buenas noches"
        };
        var greetingDetail = $"{localNow.ToString("dddd, d 'de' MMMM", culture)} · Kohana está listo";

        var (taskValue, taskDetail) = BuildTaskSummary(taskManager, now, localNow);
        var (focusValue, focusDetail, focusHasSession, focusIsPaused) =
            BuildFocusSummary(taskManager, focusManager, now);
        var (routineValue, routineDetail) = BuildRoutineSummary(routines);

        var contextTitle = hasRememberedExternalWindow
            ? "Ventana activa recordada"
            : "Lista para analizar";
        var contextDetail = hasRememberedExternalWindow
            ? "Pulsa aquí para capturarla con tu autorización."
            : "Abre una ventana y Kohana podrá verla cuando lo pidas.";

        return new HomeDashboardViewModel(
            greeting,
            greetingDetail,
            taskValue,
            taskDetail,
            focusValue,
            focusDetail,
            focusHasSession,
            focusIsPaused,
            routineValue,
            routineDetail,
            contextTitle,
            contextDetail);
    }

    private static (string Value, string Detail) BuildTaskSummary(
        TaskManager taskManager,
        DateTimeOffset now,
        DateTime localNow)
    {
        var pending = taskManager.GetAll()
            .Where(task => !task.IsCompleted)
            .OrderBy(task => task.DueAt ?? DateTimeOffset.MaxValue)
            .ThenByDescending(task => task.Priority)
            .ToArray();
        var today = pending
            .Where(task => task.DueAt.HasValue && task.DueAt.Value.LocalDateTime.Date == localNow.Date)
            .ToArray();
        var overdue = pending.Count(task => task.IsOverdue(now));

        var value = today.Length > 0
            ? today.Length.ToString(CultureInfo.InvariantCulture)
            : overdue > 0
                ? overdue.ToString(CultureInfo.InvariantCulture)
                : "0";

        var detail = today.FirstOrDefault() is { } nextToday
            ? nextToday.DueAt.HasValue
                ? $"Siguiente: {nextToday.Title} · {nextToday.DueAt.Value:HH:mm}"
                : $"Siguiente: {nextToday.Title}"
            : overdue > 0
                ? $"{overdue} vencida{(overdue == 1 ? string.Empty : "s")} necesita atención"
                : pending.FirstOrDefault() is { } nextPending
                    ? $"Próxima: {nextPending.Title}"
                    : "Todavía no tienes tareas para hoy";

        return (value, detail);
    }

    /// <summary>
    /// Diseño D3.1 — delega en <see cref="FocusDisplayStateBuilder"/>, la misma fuente que ya
    /// alimenta Enfoque y el mini temporizador global, para no mantener tres cálculos distintos
    /// del mismo reloj.
    /// </summary>
    private static (string Value, string Detail, bool HasSession, bool IsPaused) BuildFocusSummary(
        TaskManager taskManager,
        FocusManager focusManager,
        DateTimeOffset now)
    {
        var state = FocusDisplayStateBuilder.Build(
            focusManager,
            now,
            taskId => taskManager.GetAll().FirstOrDefault(task => task.Id == taskId)?.Title);

        if (state.HasSession)
        {
            var detail = state.IsPaused
                ? $"{state.AssociatedLabel} · en pausa"
                : state.AssociatedLabel ?? "Sesión de enfoque";
            return (state.ClockText, detail, true, state.IsPaused);
        }

        // Diseño D3: sin sesión activa no se muestra una duración sugerida (un "25 min" fijo se
        // leía como una medición). Se muestra el tiempo acumulado hoy si existe, o un estado vacío
        // honesto si no.
        if (state.FocusMinutesToday > 0)
        {
            return ($"{state.FocusMinutesToday} min", "Enfocados hoy", false, false);
        }

        return ("—", "No hay una sesión de enfoque activa", false, false);
    }

    private static (string Value, string Detail) BuildRoutineSummary(IReadOnlyList<RoutineDefinition> routines)
    {
        var enabled = routines.Count(routine => routine.IsEnabled);

        if (routines.Count == 0)
        {
            return ("0", "No hay rutinas configuradas");
        }

        var lastRun = routines
            .Where(routine => routine.LastExecutedAt.HasValue)
            .OrderByDescending(routine => routine.LastExecutedAt)
            .FirstOrDefault();

        var detail = lastRun is not null
            ? $"Última: {lastRun.Name} · {lastRun.LastExecutedAt:ddd HH:mm}"
            : $"{enabled} disponible{(enabled == 1 ? string.Empty : "s")}";

        return (enabled.ToString(CultureInfo.InvariantCulture), detail);
    }
}
