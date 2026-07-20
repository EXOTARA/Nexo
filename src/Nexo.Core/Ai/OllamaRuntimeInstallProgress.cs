namespace Nexo.Core.Ai;

public sealed record OllamaRuntimeInstallProgress(
    string Stage,
    string Message,
    long? CompletedBytes = null,
    long? TotalBytes = null)
{
    public double? Percentage =>
        CompletedBytes is long completed &&
        TotalBytes is long total &&
        total > 0
            ? Math.Clamp(completed * 100d / total, 0d, 100d)
            : null;
}
