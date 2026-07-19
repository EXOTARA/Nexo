using System.Text.Json.Serialization;

namespace Nexo.Core.Tasks;

public sealed class NexoTask
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset? DueAt { get; set; }

    public bool ReminderEnabled { get; set; }

    public DateTimeOffset? ReminderDeliveredAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public TaskPriority Priority { get; set; } = TaskPriority.Normal;

    [JsonIgnore]
    public bool IsCompleted => CompletedAt.HasValue;

    public bool IsOverdue(DateTimeOffset now) =>
        !IsCompleted && DueAt.HasValue && DueAt.Value < now;

    public NexoTask Copy() => new()
    {
        Id = Id,
        Title = Title,
        Notes = Notes,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt,
        DueAt = DueAt,
        ReminderEnabled = ReminderEnabled,
        ReminderDeliveredAt = ReminderDeliveredAt,
        CompletedAt = CompletedAt,
        Priority = Priority
    };
}
