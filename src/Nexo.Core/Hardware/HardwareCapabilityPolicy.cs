namespace Nexo.Core.Hardware;

public static class HardwareCapabilityPolicy
{
    private const long Gibibyte = 1024L * 1024L * 1024L;

    public const ulong StandardRamBytes = 8 * Gibibyte;
    public const ulong AcceleratedRamBytes = 16 * Gibibyte;
    public const ulong HighPerformanceRamBytes = 32 * Gibibyte;

    public const int StandardLogicalProcessors = 5;
    public const int AcceleratedLogicalProcessors = 9;
    public const int HighPerformanceLogicalProcessors = 17;

    public const long AcceleratedGraphicsMemoryBytes = 4 * Gibibyte;
    public const long HighPerformanceGraphicsMemoryBytes = 8 * Gibibyte;

    public static HardwareCapabilityProfile Evaluate(HardwareCapabilitySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshot.Processor);
        ArgumentNullException.ThrowIfNull(snapshot.Memory);
        ArgumentNullException.ThrowIfNull(snapshot.PreferredGraphicsAdapter);

        var memoryLevel = GetMemoryLevel(snapshot.Memory);
        var processorLevel = GetProcessorLevel(snapshot.Processor);
        var graphicsLevel = GetGraphicsLevel(snapshot.PreferredGraphicsAdapter);

        var knownLevels = new List<int>(3);
        if (memoryLevel is { } ml) knownLevels.Add(ml);
        if (processorLevel is { } pl) knownLevels.Add(pl);
        if (graphicsLevel is { } gl) knownLevels.Add(gl);

        HardwareCapabilityTier tier;
        HardwareDataConfidence confidence;
        string summary;

        if (knownLevels.Count == 0)
        {
            tier = HardwareCapabilityTier.Standard;
            confidence = HardwareDataConfidence.Unknown;
            summary = "No se pudo reunir suficiente información para clasificar el equipo con precisión.";
        }
        else
        {
            var averageLevel = (int)Math.Floor(knownLevels.Average());
            tier = (HardwareCapabilityTier)Math.Clamp(averageLevel, 0, 3);
            confidence = knownLevels.Count == 3
                ? HardwareDataConfidence.Known
                : HardwareDataConfidence.Estimated;
            summary = DescribeTier(tier);
        }

        var positiveReasons = BuildPositiveReasons(snapshot, memoryLevel, processorLevel, graphicsLevel);
        var limitations = BuildLimitations(snapshot, memoryLevel, processorLevel, graphicsLevel);
        var missingData = BuildMissingData(snapshot);

        return new HardwareCapabilityProfile(
            snapshot,
            tier,
            summary,
            positiveReasons,
            limitations,
            missingData,
            confidence);
    }

    private static int? GetMemoryLevel(MemoryCapability memory)
    {
        if (memory.TotalPhysicalBytes is not { } totalBytes)
        {
            return null;
        }

        if (totalBytes >= HighPerformanceRamBytes) return 3;
        if (totalBytes >= AcceleratedRamBytes) return 2;
        if (totalBytes >= StandardRamBytes) return 1;
        return 0;
    }

    private static int? GetProcessorLevel(ProcessorCapability processor)
    {
        if (processor.LogicalProcessors is not { } logicalProcessors)
        {
            return null;
        }

        if (logicalProcessors >= HighPerformanceLogicalProcessors) return 3;
        if (logicalProcessors >= AcceleratedLogicalProcessors) return 2;
        if (logicalProcessors >= StandardLogicalProcessors) return 1;
        return 0;
    }

    private static int? GetGraphicsLevel(GraphicsCapability graphics)
    {
        if (graphics.IsDedicated is not { } isDedicated)
        {
            return null;
        }

        if (!isDedicated)
        {
            return 0;
        }

        if (graphics.DedicatedMemoryBytes is not { } dedicatedMemoryBytes)
        {
            return 1;
        }

        if (dedicatedMemoryBytes >= HighPerformanceGraphicsMemoryBytes) return 3;
        if (dedicatedMemoryBytes >= AcceleratedGraphicsMemoryBytes) return 2;
        return 1;
    }

    private static string DescribeTier(HardwareCapabilityTier tier) => tier switch
    {
        HardwareCapabilityTier.Basic => "Equipo con capacidad básica.",
        HardwareCapabilityTier.Standard => "Equipo con capacidad estándar.",
        HardwareCapabilityTier.Accelerated => "Equipo con capacidad acelerada.",
        HardwareCapabilityTier.HighPerformance => "Equipo de alto rendimiento.",
        _ => "Equipo con capacidad estándar."
    };

    private static IReadOnlyList<HardwareCapabilityReason> BuildPositiveReasons(
        HardwareCapabilitySnapshot snapshot,
        int? memoryLevel,
        int? processorLevel,
        int? graphicsLevel)
    {
        var reasons = new List<HardwareCapabilityReason>();

        if (snapshot.Memory.TotalPhysicalBytes is { } totalBytes)
        {
            reasons.Add(new HardwareCapabilityReason($"{FormatGibibytes(totalBytes)} GB de RAM."));
        }

        if (snapshot.Processor.LogicalProcessors is { } logicalProcessors)
        {
            reasons.Add(new HardwareCapabilityReason($"{logicalProcessors} procesadores lógicos."));
        }

        if (snapshot.Processor.PhysicalCores is { } physicalCores)
        {
            reasons.Add(new HardwareCapabilityReason($"{physicalCores} núcleos físicos."));
        }

        if (graphicsLevel is { } gl && gl >= 1)
        {
            reasons.Add(new HardwareCapabilityReason("GPU dedicada detectada."));

            if (snapshot.PreferredGraphicsAdapter.DedicatedMemoryBytes is { } dedicatedMemoryBytes)
            {
                reasons.Add(new HardwareCapabilityReason(
                    $"{FormatGibibytes((ulong)dedicatedMemoryBytes)} GB de memoria gráfica dedicada."));
            }
        }

        if (snapshot.HasBattery is true)
        {
            reasons.Add(new HardwareCapabilityReason("Equipo portátil con batería detectada."));
        }

        return reasons;
    }

    private static IReadOnlyList<HardwareCapabilityReason> BuildLimitations(
        HardwareCapabilitySnapshot snapshot,
        int? memoryLevel,
        int? processorLevel,
        int? graphicsLevel)
    {
        var limitations = new List<HardwareCapabilityReason>();

        if (memoryLevel == 0 && snapshot.Memory.TotalPhysicalBytes is { } totalBytes)
        {
            limitations.Add(new HardwareCapabilityReason($"RAM limitada ({FormatGibibytes(totalBytes)} GB)."));
        }

        if (processorLevel == 0 && snapshot.Processor.LogicalProcessors is { } logicalProcessors)
        {
            limitations.Add(new HardwareCapabilityReason($"Pocos procesadores lógicos ({logicalProcessors})."));
        }

        if (graphicsLevel == 0)
        {
            limitations.Add(new HardwareCapabilityReason("No se detectó GPU dedicada."));
        }

        return limitations;
    }

    private static IReadOnlyList<HardwareCapabilityReason> BuildMissingData(HardwareCapabilitySnapshot snapshot)
    {
        var missingData = new List<HardwareCapabilityReason>();

        if (snapshot.Memory.TotalPhysicalBytes is null)
        {
            missingData.Add(new HardwareCapabilityReason("No se pudo determinar la RAM total."));
        }

        if (snapshot.Processor.LogicalProcessors is null)
        {
            missingData.Add(new HardwareCapabilityReason("No se pudo determinar la cantidad de procesadores lógicos."));
        }

        if (snapshot.Processor.PhysicalCores is null)
        {
            missingData.Add(new HardwareCapabilityReason("No se pudo determinar la cantidad de núcleos físicos."));
        }

        if (snapshot.PreferredGraphicsAdapter.IsDedicated is null)
        {
            missingData.Add(new HardwareCapabilityReason("No se pudo determinar si el equipo tiene una GPU dedicada."));
        }
        else if (snapshot.PreferredGraphicsAdapter.IsDedicated is true &&
                 snapshot.PreferredGraphicsAdapter.DedicatedMemoryBytes is null)
        {
            missingData.Add(new HardwareCapabilityReason("No se pudo determinar la memoria gráfica dedicada."));
        }

        if (snapshot.HasBattery is null)
        {
            missingData.Add(new HardwareCapabilityReason("No se pudo determinar si el equipo tiene batería."));
        }

        return missingData;
    }

    private static string FormatGibibytes(ulong bytes) =>
        (bytes / (double)Gibibyte).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
}
