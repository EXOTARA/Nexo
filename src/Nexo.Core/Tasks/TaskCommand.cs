namespace Nexo.Core.Tasks;

public sealed record TaskCommand(
    TaskCommandType Type,
    string OriginalText,
    string? Title = null,
    DateTimeOffset? DueAt = null,
    TaskPriority Priority = TaskPriority.Normal,
    bool ReminderEnabled = false)
{
    public static TaskCommand None(string originalText) =>
        new(TaskCommandType.None, originalText);
}
