using Nexo.Core.Commands;

namespace Nexo.Core.Tasks;

public sealed class TaskManager
{
    private readonly object _sync = new();
    private readonly ITaskStore _store;
    private readonly List<NexoTask> _tasks = [];

    public TaskManager(ITaskStore store)
    {
        _store = store;
    }

    public void Load()
    {
        lock (_sync)
        {
            _tasks.Clear();
            _tasks.AddRange(_store.Load().Select(Normalize).OrderBy(task => task.CreatedAt));
        }
    }

    public IReadOnlyList<NexoTask> GetAll()
    {
        lock (_sync)
        {
            return _tasks
                .Select(task => task.Copy())
                .OrderBy(task => task.IsCompleted)
                .ThenBy(task => task.DueAt ?? DateTimeOffset.MaxValue)
                .ThenByDescending(task => task.Priority)
                .ThenBy(task => task.CreatedAt)
                .ToArray();
        }
    }

    public NexoTask Create(
        string title,
        string? notes = null,
        DateTimeOffset? dueAt = null,
        TaskPriority priority = TaskPriority.Normal,
        bool reminderEnabled = false)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("La tarea necesita un título.", nameof(title));
        }

        var now = DateTimeOffset.Now;
        var task = new NexoTask
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            Notes = notes?.Trim() ?? string.Empty,
            CreatedAt = now,
            UpdatedAt = now,
            DueAt = dueAt,
            Priority = priority,
            ReminderEnabled = reminderEnabled && dueAt.HasValue
        };

        lock (_sync)
        {
            _tasks.Add(task);
            SaveLocked();
            return task.Copy();
        }
    }

    public TaskOperationResult Update(NexoTask updated)
    {
        lock (_sync)
        {
            var existing = _tasks.FirstOrDefault(task => task.Id == updated.Id);
            if (existing is null)
            {
                return TaskOperationResult.Failed("La tarea ya no existe.");
            }

            if (string.IsNullOrWhiteSpace(updated.Title))
            {
                return TaskOperationResult.Failed("La tarea necesita un título.");
            }

            var dueChanged = existing.DueAt != updated.DueAt;
            existing.Title = updated.Title.Trim();
            existing.Notes = updated.Notes?.Trim() ?? string.Empty;
            existing.DueAt = updated.DueAt;
            existing.Priority = updated.Priority;
            existing.ReminderEnabled = updated.ReminderEnabled && updated.DueAt.HasValue;
            existing.UpdatedAt = DateTimeOffset.Now;

            if (!existing.ReminderEnabled || dueChanged)
            {
                existing.ReminderDeliveredAt = null;
            }

            SaveLocked();
            return TaskOperationResult.Completed("Tarea actualizada.", existing.Copy());
        }
    }

    public TaskOperationResult Complete(Guid id)
    {
        lock (_sync)
        {
            var task = _tasks.FirstOrDefault(candidate => candidate.Id == id);
            if (task is null)
            {
                return TaskOperationResult.Failed("La tarea ya no existe.");
            }

            if (!task.IsCompleted)
            {
                task.CompletedAt = DateTimeOffset.Now;
                task.UpdatedAt = DateTimeOffset.Now;
                SaveLocked();
            }

            return TaskOperationResult.Completed($"Completaste: {task.Title}.", task.Copy());
        }
    }

    public TaskOperationResult CompleteMatching(string query)
    {
        lock (_sync)
        {
            var task = FindBestMatchLocked(query, includeCompleted: false);
            if (task is null)
            {
                return TaskOperationResult.Failed("No encontré una tarea pendiente con ese nombre.");
            }

            task.CompletedAt = DateTimeOffset.Now;
            task.UpdatedAt = DateTimeOffset.Now;
            SaveLocked();
            return TaskOperationResult.Completed($"Completaste: {task.Title}.", task.Copy());
        }
    }

    public TaskOperationResult Delete(Guid id)
    {
        lock (_sync)
        {
            var task = _tasks.FirstOrDefault(candidate => candidate.Id == id);
            if (task is null)
            {
                return TaskOperationResult.Failed("La tarea ya no existe.");
            }

            _tasks.Remove(task);
            SaveLocked();
            return TaskOperationResult.Completed($"Eliminé: {task.Title}.", task.Copy());
        }
    }

    public TaskOperationResult DeleteMatching(string query)
    {
        lock (_sync)
        {
            var task = FindBestMatchLocked(query, includeCompleted: true);
            if (task is null)
            {
                return TaskOperationResult.Failed("No encontré una tarea con ese nombre.");
            }

            _tasks.Remove(task);
            SaveLocked();
            return TaskOperationResult.Completed($"Eliminé: {task.Title}.", task.Copy());
        }
    }

    public IReadOnlyList<NexoTask> CollectDueReminders(DateTimeOffset now)
    {
        lock (_sync)
        {
            var due = _tasks
                .Where(task =>
                    !task.IsCompleted &&
                    task.ReminderEnabled &&
                    task.DueAt.HasValue &&
                    task.DueAt.Value <= now &&
                    !task.ReminderDeliveredAt.HasValue)
                .OrderBy(task => task.DueAt)
                .ToArray();

            if (due.Length == 0)
            {
                return [];
            }

            foreach (var task in due)
            {
                task.ReminderDeliveredAt = now;
                task.UpdatedAt = now;
            }

            SaveLocked();
            return due.Select(task => task.Copy()).ToArray();
        }
    }

    public string BuildTodaySummary(DateTimeOffset now)
    {
        lock (_sync)
        {
            var today = _tasks
                .Where(task =>
                    !task.IsCompleted &&
                    task.DueAt.HasValue &&
                    task.DueAt.Value.LocalDateTime.Date == now.LocalDateTime.Date)
                .OrderBy(task => task.DueAt)
                .ThenByDescending(task => task.Priority)
                .ToArray();

            var overdue = _tasks.Count(task => task.IsOverdue(now));
            if (today.Length == 0)
            {
                return overdue > 0
                    ? $"No tienes tareas para hoy, pero hay {overdue} vencida{(overdue == 1 ? string.Empty : "s")}."
                    : "No tienes tareas pendientes para hoy.";
            }

            var lines = today
                .Take(5)
                .Select(task =>
                    $"• {task.Title}" +
                    (task.DueAt.HasValue ? $" · {task.DueAt.Value:HH:mm}" : string.Empty));

            var summary = $"Tienes {today.Length} tarea{(today.Length == 1 ? string.Empty : "s")} para hoy:\n{string.Join("\n", lines)}";
            if (today.Length > 5)
            {
                summary += $"\nY {today.Length - 5} más.";
            }

            if (overdue > 0)
            {
                summary += $"\nTambién hay {overdue} vencida{(overdue == 1 ? string.Empty : "s")}.";
            }

            return summary;
        }
    }

    public string BuildPendingSummary(DateTimeOffset now)
    {
        lock (_sync)
        {
            var pending = _tasks
                .Where(task => !task.IsCompleted)
                .OrderBy(task => task.DueAt ?? DateTimeOffset.MaxValue)
                .ThenByDescending(task => task.Priority)
                .ToArray();

            if (pending.Length == 0)
            {
                return "No tienes tareas pendientes.";
            }

            var lines = pending
                .Take(6)
                .Select(task =>
                {
                    var schedule = task.DueAt.HasValue
                        ? task.DueAt.Value.LocalDateTime.Date == now.LocalDateTime.Date
                            ? $"hoy {task.DueAt.Value:HH:mm}"
                            : task.DueAt.Value.ToString("ddd d MMM · HH:mm")
                        : "sin fecha";
                    return $"• {task.Title} · {schedule}";
                });

            var summary = $"Tienes {pending.Length} tarea{(pending.Length == 1 ? string.Empty : "s")} pendiente{(pending.Length == 1 ? string.Empty : "s")}:\n{string.Join("\n", lines)}";
            if (pending.Length > 6)
            {
                summary += $"\nY {pending.Length - 6} más.";
            }

            return summary;
        }
    }

    private NexoTask? FindBestMatchLocked(string query, bool includeCompleted)
    {
        var normalizedQuery = Normalize(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return null;
        }

        return _tasks
            .Where(task => includeCompleted || !task.IsCompleted)
            .Select(task => new
            {
                Task = task,
                Title = Normalize(task.Title)
            })
            .Where(item =>
                item.Title.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                normalizedQuery.Contains(item.Title, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => Math.Abs(item.Title.Length - normalizedQuery.Length))
            .Select(item => item.Task)
            .FirstOrDefault();
    }

    private void SaveLocked() =>
        _store.Save(_tasks.Select(task => task.Copy()).ToArray());

    private static NexoTask Normalize(NexoTask task)
    {
        task.Title = task.Title?.Trim() ?? string.Empty;
        task.Notes = task.Notes?.Trim() ?? string.Empty;
        task.CreatedAt = task.CreatedAt == default ? DateTimeOffset.Now : task.CreatedAt;
        task.UpdatedAt = task.UpdatedAt == default ? task.CreatedAt : task.UpdatedAt;
        task.ReminderEnabled = task.ReminderEnabled && task.DueAt.HasValue;
        return task;
    }

    private static string Normalize(string value) =>
        NaturalCommandParser.Normalize(value);
}
