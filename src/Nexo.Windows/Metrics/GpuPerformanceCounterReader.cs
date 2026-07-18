using System.Collections;
using System.Diagnostics;

namespace Nexo.Windows.Metrics;

internal sealed class GpuPerformanceCounterReader
{
    private const string EngineCategoryName = "GPU Engine";
    private const string EngineCounterName = "Utilization Percentage";
    private const string MemoryCategoryName = "GPU Adapter Memory";
    private const string DedicatedMemoryCounterName = "Dedicated Usage";

    private readonly object _sync = new();
    private Dictionary<string, CounterSample> _previousEngineSamples =
        new(StringComparer.OrdinalIgnoreCase);

    public GpuMetrics Read()
    {
        lock (_sync)
        {
            return new GpuMetrics(
                UsagePercent: ReadGpuUsage(),
                DedicatedMemoryBytes: ReadDedicatedMemory());
        }
    }

    private double? ReadGpuUsage()
    {
        try
        {
            var category = new PerformanceCounterCategory(EngineCategoryName);
            var categoryData = category.ReadCategory();
            var utilization = categoryData[EngineCounterName];
            if (utilization is null)
            {
                return null;
            }

            var currentSamples = new Dictionary<string, CounterSample>(
                StringComparer.OrdinalIgnoreCase);
            var engineTotals = new Dictionary<string, double>(
                StringComparer.OrdinalIgnoreCase);

            foreach (DictionaryEntry entry in utilization)
            {
                if (entry.Key is not string instanceName || entry.Value is not InstanceData instanceData)
                {
                    continue;
                }

                var current = instanceData.Sample;
                currentSamples[instanceName] = current;

                if (!_previousEngineSamples.TryGetValue(instanceName, out var previous))
                {
                    continue;
                }

                float value;
                try
                {
                    value = CounterSample.Calculate(previous, current);
                }
                catch (InvalidOperationException)
                {
                    continue;
                }

                if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0)
                {
                    continue;
                }

                var engineKey = GetEngineKey(instanceName);
                engineTotals.TryGetValue(engineKey, out var total);
                engineTotals[engineKey] = total + value;
            }

            _previousEngineSamples = currentSamples;

            if (engineTotals.Count == 0)
            {
                return null;
            }

            // Los contadores son por proceso y motor. Sumamos los procesos que
            // comparten un mismo motor y mostramos el motor más ocupado.
            return Math.Clamp(engineTotals.Values.Max(), 0, 100);
        }
        catch (Exception exception) when (exception is
            InvalidOperationException or
            UnauthorizedAccessException or
            System.ComponentModel.Win32Exception)
        {
            _previousEngineSamples.Clear();
            return null;
        }
    }

    private static long? ReadDedicatedMemory()
    {
        try
        {
            var category = new PerformanceCounterCategory(MemoryCategoryName);
            var categoryData = category.ReadCategory();
            var dedicatedUsage = categoryData[DedicatedMemoryCounterName];
            if (dedicatedUsage is null)
            {
                return null;
            }

            long total = 0;
            var found = false;

            foreach (DictionaryEntry entry in dedicatedUsage)
            {
                if (entry.Value is not InstanceData instanceData)
                {
                    continue;
                }

                var value = instanceData.Sample.RawValue;
                if (value < 0)
                {
                    continue;
                }

                found = true;
                total = total > long.MaxValue - value
                    ? long.MaxValue
                    : total + value;
            }

            return found ? total : null;
        }
        catch (Exception exception) when (exception is
            InvalidOperationException or
            UnauthorizedAccessException or
            System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static string GetEngineKey(string instanceName)
    {
        var luidIndex = instanceName.IndexOf("_luid_", StringComparison.OrdinalIgnoreCase);
        var start = luidIndex >= 0 ? luidIndex + 1 : 0;
        var typeIndex = instanceName.IndexOf("_engtype_", start, StringComparison.OrdinalIgnoreCase);

        return typeIndex > start
            ? instanceName[start..typeIndex]
            : instanceName[start..];
    }
}

internal readonly record struct GpuMetrics(
    double? UsagePercent,
    long? DedicatedMemoryBytes);
