using Nexo.Core.Metrics;

namespace Nexo.Core.Tests;

public sealed class SystemHealthEvaluatorTests
{
    [Fact]
    public void Evaluate_ReturnsReadyForLowUsage()
    {
        var snapshot = CreateSnapshot(cpu: 24, memory: 45, disk: 62);

        var state = SystemHealthEvaluator.Evaluate(snapshot);

        Assert.Equal(SystemHealthState.Ready, state);
    }

    [Fact]
    public void Evaluate_ReturnsModerateForElevatedUsage()
    {
        var snapshot = CreateSnapshot(cpu: 74, memory: 48, disk: 60);

        var state = SystemHealthEvaluator.Evaluate(snapshot);

        Assert.Equal(SystemHealthState.Moderate, state);
    }

    [Fact]
    public void Evaluate_ReturnsBusyForVeryHighUsage()
    {
        var snapshot = CreateSnapshot(cpu: 95, memory: 50, disk: 60);

        var state = SystemHealthEvaluator.Evaluate(snapshot);

        Assert.Equal(SystemHealthState.Busy, state);
    }

    [Fact]
    public void Evaluate_ReturnsModerateForHighGpuUsage()
    {
        var snapshot = new SystemSnapshot(
            CpuUsagePercent: 20,
            MemoryUsagePercent: 40,
            UsedMemoryBytes: 8,
            TotalMemoryBytes: 16,
            GpuUsagePercent: 85,
            DedicatedGpuMemoryBytes: 1024,
            SystemDriveUsagePercent: 60,
            TopProcessName: "test",
            TopProcessWorkingSetBytes: 100,
            CapturedAt: DateTimeOffset.UtcNow);

        var state = SystemHealthEvaluator.Evaluate(snapshot);

        Assert.Equal(SystemHealthState.Moderate, state);
    }

    private static SystemSnapshot CreateSnapshot(double cpu, double memory, double disk)
    {
        return new SystemSnapshot(
            CpuUsagePercent: cpu,
            MemoryUsagePercent: memory,
            UsedMemoryBytes: 8,
            TotalMemoryBytes: 16,
            GpuUsagePercent: 20,
            DedicatedGpuMemoryBytes: 0,
            SystemDriveUsagePercent: disk,
            TopProcessName: "test",
            TopProcessWorkingSetBytes: 100,
            CapturedAt: DateTimeOffset.UtcNow);
    }
}
