using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Nexo.App.DailyFlow;
using Nexo.Core.Focus;

namespace Nexo.App.Views;

public partial class FocusView : UserControl
{
    private readonly FocusManager _focusManager;
    private readonly Func<Guid, string?>? _resolveTaskTitle;
    private Guid? _pendingAssociatedTaskId;

    public FocusView(FocusManager focusManager, Func<Guid, string?>? resolveTaskTitle = null)
    {
        _focusManager = focusManager;
        _resolveTaskTitle = resolveTaskTitle;
        InitializeComponent();
        Refresh(DateTimeOffset.Now);
    }

    public event EventHandler? FocusChanged;

    /// <summary>
    /// Diseño D3 — el usuario pidió marcar como completada la tarea que estaba asociada a la
    /// sesión que acaba de terminar o finalizarse. Nunca se dispara sola: siempre en respuesta a
    /// pulsar "Marcar como completada" en <see cref="TaskCompletionPromptBorder"/>.
    /// </summary>
    public event EventHandler<TaskFocusRequestedEventArgs>? CompleteAssociatedTaskRequested;

    public void Refresh(DateTimeOffset now)
    {
        var snapshot = _focusManager.GetSnapshot(now);
        var timer = snapshot.ActiveTimer;

        TodaySessionsText.Text = $"{snapshot.CompletedSessionsToday} " +
            (snapshot.CompletedSessionsToday == 1 ? "sesión" : "sesiones");
        TodayMinutesText.Text = $"{snapshot.FocusMinutesToday} min";

        RefreshHistory(now);

        if (timer is null)
        {
            TimerStateText.Text = "LISTO";
            TimerLabelText.Text = "Sin sesión activa";
            RemainingTimeText.Text = "00:00";
            TimerDetailText.Text = "Elige una duración para comenzar.";
            TimerProgressBar.Value = 0;
            PauseButton.IsEnabled = false;
            ResumeButton.IsEnabled = false;
            FinishButton.IsEnabled = false;
            CancelButton.IsEnabled = false;
            AssociatedTaskBadge.Visibility = Visibility.Collapsed;
            return;
        }

        var remaining = snapshot.Remaining;
        var elapsed = timer.Duration - remaining;
        var progress = timer.Duration.TotalSeconds <= 0
            ? 0
            : Math.Clamp(elapsed.TotalSeconds / timer.Duration.TotalSeconds * 100, 0, 100);

        TimerStateText.Text = timer.Status == FocusTimerStatus.Paused
            ? "EN PAUSA"
            : "EN CURSO";
        TimerLabelText.Text = timer.Label;
        RemainingTimeText.Text = FormatClock(remaining);
        TimerDetailText.Text = timer.Status == FocusTimerStatus.Paused
            ? "La sesión está pausada y conservará este tiempo."
            : $"Finaliza aproximadamente a las {timer.EndsAt:HH:mm}.";
        TimerProgressBar.Value = progress;
        PauseButton.IsEnabled = timer.Status == FocusTimerStatus.Running;
        ResumeButton.IsEnabled = timer.Status == FocusTimerStatus.Paused;
        FinishButton.IsEnabled = true;
        CancelButton.IsEnabled = true;

        // La tarea asociada se resuelve por Id cada vez (no se guarda el título en el timer):
        // así, si Kohana se reinicia a mitad de una sesión asociada, el título mostrado sigue
        // siendo el actual de la tarea, no uno congelado del momento en que empezó.
        var taskTitle = timer.TaskId.HasValue ? _resolveTaskTitle?.Invoke(timer.TaskId.Value) : null;
        if (!string.IsNullOrWhiteSpace(taskTitle))
        {
            AssociatedTaskText.Text = $"Enfocándote en: {taskTitle}";
            AssociatedTaskBadge.Visibility = Visibility.Visible;
        }
        else
        {
            AssociatedTaskBadge.Visibility = Visibility.Collapsed;
        }
    }

    public void FocusPrimaryControl()
    {
        if (_focusManager.GetSnapshot(DateTimeOffset.Now).ActiveTimer is null)
        {
            CustomMinutesTextBox.Focus();
        }
    }

    /// <summary>
    /// Diseño D3.1 — inicia un preset de enfoque desde fuera de la vista (Command Center). Reusa
    /// el mismo <see cref="Start"/> privado que los botones de "Inicio rápido", así una tarea
    /// pendiente de asociar (ver <see cref="PrepareTaskAssociation"/>) no se pierde solo porque el
    /// usuario haya iniciado la sesión desde Ctrl + K en vez de tocar un botón.
    /// </summary>
    public void StartPreset(TimeSpan duration) => Start(duration, FocusSessionKind.Focus);

    /// <summary>
    /// Diseño D3 — llamado antes de navegar aquí desde "Enfocarme" en Hoy. La siguiente sesión que
    /// se inicie (preset o duración personalizada) queda asociada a esta tarea; no inicia nada por
    /// sí solo.
    /// </summary>
    public void PrepareTaskAssociation(Guid taskId, string taskTitle)
    {
        _pendingAssociatedTaskId = taskId;
        ActionStatusText.Text = $"Elige una duración para enfocarte en «{taskTitle}».";
    }

    /// <summary>
    /// Diseño D3 — muestra la sugerencia (no automática) de completar la tarea asociada a una
    /// sesión que acaba de terminar, ya sea de forma natural o por "Finalizar".
    /// </summary>
    public void ShowTaskCompletionPrompt(Guid taskId, string taskTitle)
    {
        _pendingCompletionTaskId = taskId;
        _pendingCompletionTaskTitle = taskTitle;
        TaskCompletionPromptText.Text = $"Terminaste tu sesión enfocado en «{taskTitle}». ¿Marcarla como completada?";
        TaskCompletionPromptBorder.Visibility = Visibility.Visible;
    }

    private Guid? _pendingCompletionTaskId;
    private string _pendingCompletionTaskTitle = string.Empty;

    private void DismissTaskCompletionButton_Click(object sender, RoutedEventArgs e) =>
        HideTaskCompletionPrompt();

    private void CompleteAssociatedTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingCompletionTaskId is not { } taskId)
        {
            HideTaskCompletionPrompt();
            return;
        }

        var title = _pendingCompletionTaskTitle;
        HideTaskCompletionPrompt();
        CompleteAssociatedTaskRequested?.Invoke(this, new TaskFocusRequestedEventArgs(taskId, title));
    }

    private void HideTaskCompletionPrompt()
    {
        _pendingCompletionTaskId = null;
        _pendingCompletionTaskTitle = string.Empty;
        TaskCompletionPromptBorder.Visibility = Visibility.Collapsed;
    }

    private void PresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag })
        {
            return;
        }

        var parts = tag.Split(':', 2);
        if (parts.Length != 2 ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes))
        {
            return;
        }

        var kind = parts[0].Equals("Break", StringComparison.OrdinalIgnoreCase)
            ? FocusSessionKind.Break
            : FocusSessionKind.Focus;
        Start(TimeSpan.FromMinutes(minutes), kind);
    }

    private void StartCustomButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(
                CustomMinutesTextBox.Text.Trim(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var minutes) ||
            minutes is < 1 or > 1440)
        {
            ActionStatusText.Text = "Escribe una duración entre 1 y 1440 minutos.";
            return;
        }

        Start(TimeSpan.FromMinutes(minutes), FocusSessionKind.Custom);
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e) =>
        Apply(_focusManager.Pause(DateTimeOffset.Now));

    private void ResumeButton_Click(object sender, RoutedEventArgs e) =>
        Apply(_focusManager.Resume(DateTimeOffset.Now));

    private void FinishButton_Click(object sender, RoutedEventArgs e)
    {
        var now = DateTimeOffset.Now;
        // Se lee el TaskId ANTES de finalizar: Finish() limpia el temporizador activo, así que
        // después ya no habría forma de saber a qué tarea pertenecía.
        var timer = _focusManager.GetSnapshot(now).ActiveTimer;
        var taskId = timer?.TaskId;

        var result = _focusManager.Finish(now);
        Apply(result);

        if (result.Success && taskId is { } id)
        {
            var title = _resolveTaskTitle?.Invoke(id);
            if (!string.IsNullOrWhiteSpace(title))
            {
                ShowTaskCompletionPrompt(id, title);
            }
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        // Cancelar descarta la sesión sin registrarla: no tiene sentido ofrecer completar la
        // tarea asociada a algo que ni siquiera queda en el historial.
        HideTaskCompletionPrompt();
        Apply(_focusManager.Cancel());
    }

    private void Start(TimeSpan duration, FocusSessionKind kind)
    {
        var label = kind switch
        {
            FocusSessionKind.Break => "Descanso",
            FocusSessionKind.Focus => "Sesión de enfoque",
            _ => "Temporizador"
        };

        var taskId = _pendingAssociatedTaskId;
        _pendingAssociatedTaskId = null;

        Apply(_focusManager.Start(
            duration,
            label,
            kind,
            DateTimeOffset.Now,
            taskId));
    }

    private void Apply(FocusOperationResult result)
    {
        ActionStatusText.Text = result.Message;
        Refresh(DateTimeOffset.Now);
        FocusChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string FormatClock(TimeSpan remaining)
    {
        var totalHours = (int)remaining.TotalHours;
        return totalHours > 0
            ? $"{totalHours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}"
            : $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
    }

    /// <summary>
    /// Diseño D3.1 — resumen real de actividad reciente, usando solo el historial que
    /// <see cref="FocusManager"/> ya guarda (nada inventado: sin rachas ni puntajes).
    /// </summary>
    private void RefreshHistory(DateTimeOffset now)
    {
        var history = _focusManager.GetHistory();
        var summary = FocusHistorySummaryBuilder.Build(history, now, _resolveTaskTitle);

        if (summary.HasTopTask)
        {
            TopTaskText.Text = $"Tarea con más tiempo hoy: {summary.TopTaskLabel} " +
                $"({summary.TopTaskMinutesToday} min)";
            TopTaskText.Visibility = Visibility.Visible;
        }
        else
        {
            TopTaskText.Visibility = Visibility.Collapsed;
        }

        var recentItems = summary.RecentSessions
            .Select(session => new RecentSessionItem(
                session.TaskTitle is null ? session.Label : $"{session.Label} · {session.TaskTitle}",
                session.TimestampText,
                session.DurationText))
            .ToArray();
        RecentSessionsItemsControl.ItemsSource = recentItems;
        RecentSessionsEmptyText.Visibility = recentItems.Length == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private sealed record RecentSessionItem(string TitleText, string SubtitleText, string DurationText);
}
