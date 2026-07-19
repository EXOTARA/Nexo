using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Nexo.Core.Tasks;

namespace Nexo.App.Views;

public partial class TasksView : UserControl
{
    private readonly TaskManager _taskManager;
    private TaskFilter _filter = TaskFilter.Today;
    private Guid? _editingId;

    public TasksView(TaskManager taskManager)
    {
        _taskManager = taskManager;
        InitializeComponent();
        Refresh();
    }

    public event EventHandler? TasksChanged;

    public void Refresh()
    {
        var now = DateTimeOffset.Now;
        var tasks = _taskManager.GetAll();

        TodayCountText.Text = tasks.Count(task =>
            !task.IsCompleted &&
            task.DueAt.HasValue &&
            task.DueAt.Value.LocalDateTime.Date == now.LocalDateTime.Date).ToString(CultureInfo.InvariantCulture);
        PendingCountText.Text = tasks.Count(task => !task.IsCompleted).ToString(CultureInfo.InvariantCulture);
        OverdueCountText.Text = tasks.Count(task => task.IsOverdue(now)).ToString(CultureInfo.InvariantCulture);

        var visible = tasks
            .Where(task => _filter switch
            {
                TaskFilter.Today =>
                    !task.IsCompleted &&
                    task.DueAt.HasValue &&
                    task.DueAt.Value.LocalDateTime.Date == now.LocalDateTime.Date,
                TaskFilter.Pending => !task.IsCompleted,
                TaskFilter.Completed => task.IsCompleted,
                _ => false
            })
            .Select(task => CreateListItem(task, now))
            .ToArray();

        TasksItemsControl.ItemsSource = visible;
        EmptyStateText.Visibility = visible.Length == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        UpdateFilterStyles();
    }

    public void OpenNewEditor()
    {
        _editingId = null;
        EditorTitleText.Text = "Nueva tarea";
        TitleTextBox.Text = string.Empty;
        NotesTextBox.Text = string.Empty;
        DueDatePicker.SelectedDate = null;
        DueTimeTextBox.Text = "09:00";
        PriorityComboBox.SelectedIndex = 1;
        ReminderCheckBox.IsChecked = false;
        HideEditorError();
        EditorBorder.Visibility = Visibility.Visible;
        TitleTextBox.Focus();
    }

    public void FocusPrimaryControl()
    {
        if (EditorBorder.Visibility == Visibility.Visible)
        {
            TitleTextBox.Focus();
        }
    }

    private void NewTaskButton_Click(object sender, RoutedEventArgs e) =>
        OpenNewEditor();

    private void CancelEditorButton_Click(object sender, RoutedEventArgs e) =>
        EditorBorder.Visibility = Visibility.Collapsed;

    private void SaveTaskButton_Click(object sender, RoutedEventArgs e)
    {
        var title = TitleTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            ShowEditorError("Escribe un título para la tarea.");
            return;
        }

        if (!TryGetDueAt(out var dueAt, out var dateError))
        {
            ShowEditorError(dateError);
            return;
        }

        var priority = GetSelectedPriority();
        var reminderEnabled = ReminderCheckBox.IsChecked == true;
        if (reminderEnabled && !dueAt.HasValue)
        {
            ShowEditorError("Elige una fecha para poder activar el recordatorio.");
            return;
        }

        if (reminderEnabled && dueAt <= DateTimeOffset.Now)
        {
            ShowEditorError("El recordatorio debe quedar en una fecha futura.");
            return;
        }

        if (_editingId.HasValue)
        {
            var existing = _taskManager.GetAll().FirstOrDefault(task => task.Id == _editingId.Value);
            if (existing is null)
            {
                ShowEditorError("La tarea ya no existe.");
                return;
            }

            existing.Title = title;
            existing.Notes = NotesTextBox.Text;
            existing.DueAt = dueAt;
            existing.Priority = priority;
            existing.ReminderEnabled = reminderEnabled;
            var result = _taskManager.Update(existing);
            if (!result.Success)
            {
                ShowEditorError(result.Message);
                return;
            }
        }
        else
        {
            _taskManager.Create(
                title,
                NotesTextBox.Text,
                dueAt,
                priority,
                reminderEnabled);
        }

        _filter = dueAt.HasValue &&
                  dueAt.Value.LocalDateTime.Date == DateTime.Today
            ? TaskFilter.Today
            : TaskFilter.Pending;
        EditorBorder.Visibility = Visibility.Collapsed;
        Refresh();
        TasksChanged?.Invoke(this, EventArgs.Empty);
    }

    private void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string filter })
        {
            return;
        }

        _filter = filter switch
        {
            "Pending" => TaskFilter.Pending,
            "Completed" => TaskFilter.Completed,
            _ => TaskFilter.Today
        };
        Refresh();
    }

    private void CompleteTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaskId(sender, out var id))
        {
            return;
        }

        _taskManager.Complete(id);
        Refresh();
        TasksChanged?.Invoke(this, EventArgs.Empty);
    }

    private void EditTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaskId(sender, out var id))
        {
            return;
        }

        var task = _taskManager.GetAll().FirstOrDefault(candidate => candidate.Id == id);
        if (task is null)
        {
            Refresh();
            return;
        }

        _editingId = id;
        EditorTitleText.Text = "Editar tarea";
        TitleTextBox.Text = task.Title;
        NotesTextBox.Text = task.Notes;
        DueDatePicker.SelectedDate = task.DueAt?.LocalDateTime.Date;
        DueTimeTextBox.Text = task.DueAt?.ToString("HH:mm") ?? "09:00";
        PriorityComboBox.SelectedIndex = task.Priority switch
        {
            TaskPriority.Low => 0,
            TaskPriority.High => 2,
            _ => 1
        };
        ReminderCheckBox.IsChecked = task.ReminderEnabled;
        HideEditorError();
        EditorBorder.Visibility = Visibility.Visible;
        TitleTextBox.Focus();
    }

    private void DeleteTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetTaskId(sender, out var id))
        {
            return;
        }

        _taskManager.Delete(id);
        Refresh();
        TasksChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool TryGetDueAt(out DateTimeOffset? dueAt, out string error)
    {
        dueAt = null;
        error = string.Empty;

        if (!DueDatePicker.SelectedDate.HasValue)
        {
            return true;
        }

        if (!TimeSpan.TryParseExact(
                DueTimeTextBox.Text.Trim(),
                ["h\\:mm", "hh\\:mm"],
                CultureInfo.InvariantCulture,
                TimeSpanStyles.None,
                out var time))
        {
            error = "Usa una hora como 09:00 o 18:30.";
            return false;
        }

        if (time < TimeSpan.Zero || time >= TimeSpan.FromDays(1))
        {
            error = "La hora no es válida.";
            return false;
        }

        var local = DateTime.SpecifyKind(
            DueDatePicker.SelectedDate.Value.Date.Add(time),
            DateTimeKind.Unspecified);
        dueAt = new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
        return true;
    }

    private TaskListItem CreateListItem(NexoTask task, DateTimeOffset now)
    {
        var schedule = task.CompletedAt.HasValue
            ? $"Completada {task.CompletedAt.Value:ddd d MMM · HH:mm}"
            : task.DueAt.HasValue
                ? FormatDueAt(task.DueAt.Value, now)
                : "Sin fecha";

        var scheduleBrush = task.IsOverdue(now)
            ? (Brush)FindResource("BrushWarning")
            : (Brush)FindResource("BrushTextSecondary");

        return new TaskListItem(
            task.Id,
            task.Title,
            task.Notes,
            schedule,
            scheduleBrush,
            task.Priority switch
            {
                TaskPriority.High => "ALTA",
                TaskPriority.Low => "BAJA",
                _ => "NORMAL"
            },
            string.IsNullOrWhiteSpace(task.Notes) ? Visibility.Collapsed : Visibility.Visible,
            task.IsCompleted ? Visibility.Collapsed : Visibility.Visible,
            task.IsCompleted ? TextDecorations.Strikethrough : null);
    }

    private static string FormatDueAt(DateTimeOffset dueAt, DateTimeOffset now)
    {
        var localDate = dueAt.LocalDateTime.Date;
        var today = now.LocalDateTime.Date;
        if (localDate == today)
        {
            return $"Hoy · {dueAt:HH:mm}";
        }

        if (localDate == today.AddDays(1))
        {
            return $"Mañana · {dueAt:HH:mm}";
        }

        return dueAt.ToString("ddd d MMM · HH:mm", new CultureInfo("es-MX"));
    }

    private TaskPriority GetSelectedPriority()
    {
        return PriorityComboBox.SelectedItem is ComboBoxItem { Tag: string value } &&
               Enum.TryParse<TaskPriority>(value, ignoreCase: true, out var priority)
            ? priority
            : TaskPriority.Normal;
    }

    private void UpdateFilterStyles()
    {
        ApplyFilterStyle(TodayFilterButton, _filter == TaskFilter.Today);
        ApplyFilterStyle(PendingFilterButton, _filter == TaskFilter.Pending);
        ApplyFilterStyle(CompletedFilterButton, _filter == TaskFilter.Completed);
    }

    private void ApplyFilterStyle(Button button, bool selected)
    {
        button.Background = selected
            ? (Brush)FindResource("BrushAccentSoft")
            : Brushes.Transparent;
        button.Foreground = selected
            ? (Brush)FindResource("BrushTextPrimary")
            : (Brush)FindResource("BrushTextSecondary");
    }

    private void ShowEditorError(string message)
    {
        EditorErrorText.Text = message;
        EditorErrorText.Visibility = Visibility.Visible;
    }

    private void HideEditorError()
    {
        EditorErrorText.Text = string.Empty;
        EditorErrorText.Visibility = Visibility.Collapsed;
    }

    private static bool TryGetTaskId(object sender, out Guid id)
    {
        id = Guid.Empty;
        return sender is Button { Tag: Guid taskId } && (id = taskId) != Guid.Empty;
    }

    private enum TaskFilter
    {
        Today,
        Pending,
        Completed
    }

    private sealed record TaskListItem(
        Guid Id,
        string Title,
        string Notes,
        string ScheduleText,
        Brush ScheduleBrush,
        string PriorityLabel,
        Visibility NotesVisibility,
        Visibility CompleteVisibility,
        TextDecorationCollection? TextDecorations);
}
