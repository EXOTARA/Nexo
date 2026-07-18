namespace Nexo.Core.Metrics;

public sealed record SystemSnapshot(
    double? CpuUsagePercent,
    double MemoryUsagePercent,
    ulong UsedMemoryBytes,
    ulong TotalMemoryBytes,
    double? GpuUsagePercent,
    long? DedicatedGpuMemoryBytes,
    double? SystemDriveUsagePercent,
    string? TopProcessName,
    long? TopProcessWorkingSetBytes,
    DateTimeOffset CapturedAt)
{
    public static SystemSnapshot Empty { get; } = new(
        CpuUsagePercent: null,
        MemoryUsagePercent: 0,
        UsedMemoryBytes: 0,
        TotalMemoryBytes: 0,
        GpuUsagePercent: null,
        DedicatedGpuMemoryBytes: null,
        SystemDriveUsagePercent: null,
        TopProcessName: null,
        TopProcessWorkingSetBytes: null,
        CapturedAt: DateTimeOffset.MinValue);
}
