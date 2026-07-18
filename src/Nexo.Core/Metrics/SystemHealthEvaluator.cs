namespace Nexo.Core.Metrics;

public enum SystemHealthState
{
    Ready,
    Moderate,
    Busy
}

public static class SystemHealthEvaluator
{
    public static SystemHealthState Evaluate(SystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (IsAtLeast(snapshot.CpuUsagePercent, 90)
            || snapshot.MemoryUsagePercent >= 92
            || IsAtLeast(snapshot.GpuUsagePercent, 95)
            || IsAtLeast(snapshot.SystemDriveUsagePercent, 97))
        {
            return SystemHealthState.Busy;
        }

        if (IsAtLeast(snapshot.CpuUsagePercent, 70)
            || snapshot.MemoryUsagePercent >= 80
            || IsAtLeast(snapshot.GpuUsagePercent, 80)
            || IsAtLeast(snapshot.SystemDriveUsagePercent, 90))
        {
            return SystemHealthState.Moderate;
        }

        return SystemHealthState.Ready;
    }

    public static string GetLabel(SystemHealthState state) => state switch
    {
        SystemHealthState.Ready => "Estable",
        SystemHealthState.Moderate => "Atención",
        SystemHealthState.Busy => "Carga alta",
        _ => "Sin datos"
    };

    private static bool IsAtLeast(double? value, double threshold)
    {
        return value.HasValue && value.Value >= threshold;
    }
}
