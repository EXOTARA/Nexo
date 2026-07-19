namespace Nexo.Core.Tasks;

public sealed record TaskOperationResult(
    bool Success,
    string Message,
    NexoTask? Task = null)
{
    public static TaskOperationResult Completed(string message, NexoTask? task = null) =>
        new(true, message, task);

    public static TaskOperationResult Failed(string message) =>
        new(false, message);
}
