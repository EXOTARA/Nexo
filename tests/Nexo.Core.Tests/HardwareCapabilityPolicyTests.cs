using Nexo.Core.Hardware;

namespace Nexo.Core.Tests;

public sealed class HardwareCapabilityPolicyTests
{
    private const ulong Gib = 1024UL * 1024UL * 1024UL;

    [Fact]
    public void BasicMachine_ClassifiesAsBasic()
    {
        var snapshot = CreateSnapshot(
            totalRamBytes: 4 * Gib,
            logicalProcessors: 4,
            physicalCores: 2,
            isDedicatedGpu: false,
            hasBattery: false);

        var profile = HardwareCapabilityPolicy.Evaluate(snapshot);

        Assert.Equal(HardwareCapabilityTier.Basic, profile.Tier);
        Assert.Equal(HardwareDataConfidence.Known, profile.OverallConfidence);
        Assert.Empty(profile.MissingData);
    }

    [Fact]
    public void StandardMachine_ClassifiesAsStandard()
    {
        var snapshot = CreateSnapshot(
            totalRamBytes: 8 * Gib,
            logicalProcessors: 8,
            physicalCores: 4,
            isDedicatedGpu: true,
            dedicatedGraphicsMemoryBytes: 1L * (long)Gib,
            hasBattery: false);

        var profile = HardwareCapabilityPolicy.Evaluate(snapshot);

        Assert.Equal(HardwareCapabilityTier.Standard, profile.Tier);
        Assert.Empty(profile.MissingData);
    }

    [Fact]
    public void MachineWithDedicatedGpu_ClassifiesAsAccelerated()
    {
        var snapshot = CreateSnapshot(
            totalRamBytes: 32 * Gib,
            logicalProcessors: 12,
            physicalCores: 6,
            isDedicatedGpu: true,
            dedicatedGraphicsMemoryBytes: 8L * (long)Gib);

        var profile = HardwareCapabilityPolicy.Evaluate(snapshot);

        Assert.Equal(HardwareCapabilityTier.Accelerated, profile.Tier);
        Assert.Contains(profile.PositiveReasons, r => r.Message == "GPU dedicada detectada.");
    }

    [Fact]
    public void HighPerformanceMachine_ClassifiesAsHighPerformance()
    {
        var snapshot = CreateSnapshot(
            totalRamBytes: 64 * Gib,
            logicalProcessors: 24,
            physicalCores: 16,
            isDedicatedGpu: true,
            dedicatedGraphicsMemoryBytes: 16L * (long)Gib);

        var profile = HardwareCapabilityPolicy.Evaluate(snapshot);

        Assert.Equal(HardwareCapabilityTier.HighPerformance, profile.Tier);
    }

    [Fact]
    public void UnknownRam_DoesNotEqualZero_ClassificationUsesRemainingKnownData()
    {
        var snapshot = CreateSnapshot(
            totalRamBytes: null,
            logicalProcessors: 24,
            physicalCores: 16,
            isDedicatedGpu: true,
            dedicatedGraphicsMemoryBytes: 16L * (long)Gib);

        var profile = HardwareCapabilityPolicy.Evaluate(snapshot);

        Assert.Equal(HardwareCapabilityTier.HighPerformance, profile.Tier);
        Assert.Equal(HardwareDataConfidence.Estimated, profile.OverallConfidence);
        Assert.Contains(profile.MissingData, r => r.Message == "No se pudo determinar la RAM total.");
    }

    [Fact]
    public void UnknownGpuPresence_DoesNotEqualZero_ClassificationUsesRemainingKnownData()
    {
        var snapshot = CreateSnapshot(
            totalRamBytes: 64 * Gib,
            logicalProcessors: 24,
            physicalCores: 16,
            isDedicatedGpu: null);

        var profile = HardwareCapabilityPolicy.Evaluate(snapshot);

        Assert.Equal(HardwareCapabilityTier.HighPerformance, profile.Tier);
        Assert.Contains(profile.MissingData, r => r.Message == "No se pudo determinar si el equipo tiene una GPU dedicada.");
    }

    [Fact]
    public void UnknownVram_DoesNotEqualZero_DedicatedGpuStillCountsAboveNone()
    {
        var snapshot = CreateSnapshot(
            totalRamBytes: 4 * Gib,
            logicalProcessors: 4,
            physicalCores: 2,
            isDedicatedGpu: true,
            dedicatedGraphicsMemoryBytes: null);

        var profile = HardwareCapabilityPolicy.Evaluate(snapshot);

        Assert.Contains(profile.MissingData, r => r.Message == "No se pudo determinar la memoria gráfica dedicada.");
        Assert.DoesNotContain(profile.Limitations, r => r.Message == "No se detectó GPU dedicada.");
    }

    [Fact]
    public void UnknownPhysicalCores_DoesNotAffectTier()
    {
        var withCores = CreateSnapshot(
            totalRamBytes: 32 * Gib,
            logicalProcessors: 12,
            physicalCores: 6,
            isDedicatedGpu: true,
            dedicatedGraphicsMemoryBytes: 8L * (long)Gib);

        var withoutCores = CreateSnapshot(
            totalRamBytes: 32 * Gib,
            logicalProcessors: 12,
            physicalCores: null,
            isDedicatedGpu: true,
            dedicatedGraphicsMemoryBytes: 8L * (long)Gib);

        var profileWithCores = HardwareCapabilityPolicy.Evaluate(withCores);
        var profileWithoutCores = HardwareCapabilityPolicy.Evaluate(withoutCores);

        Assert.Equal(profileWithCores.Tier, profileWithoutCores.Tier);
        Assert.Contains(profileWithoutCores.MissingData, r => r.Message == "No se pudo determinar la cantidad de núcleos físicos.");
    }

    [Fact]
    public void NoKnownData_DoesNotDefaultToBasic()
    {
        var profile = HardwareCapabilityPolicy.Evaluate(HardwareCapabilitySnapshot.Empty);

        Assert.NotEqual(HardwareCapabilityTier.Basic, profile.Tier);
        Assert.Equal(HardwareDataConfidence.Unknown, profile.OverallConfidence);
    }

    [Theory]
    [InlineData(HardwareCapabilityTier.Basic)]
    [InlineData(HardwareCapabilityTier.Standard)]
    [InlineData(HardwareCapabilityTier.Accelerated)]
    [InlineData(HardwareCapabilityTier.HighPerformance)]
    public void ExactTierThresholds_ClassifyAtTheLowerBound(HardwareCapabilityTier tier)
    {
        var snapshot = CreateUniformSnapshotAtTier(tier);

        var profile = HardwareCapabilityPolicy.Evaluate(snapshot);

        Assert.Equal(tier, profile.Tier);
    }

    [Fact]
    public void JustBelowAcceleratedThreshold_ClassifiesOneStepLower()
    {
        var snapshot = CreateSnapshot(
            totalRamBytes: 16 * Gib - 1,
            logicalProcessors: 9,
            physicalCores: 8,
            isDedicatedGpu: true,
            dedicatedGraphicsMemoryBytes: 4L * (long)Gib);

        var profile = HardwareCapabilityPolicy.Evaluate(snapshot);

        Assert.Equal(HardwareCapabilityTier.Standard, profile.Tier);
    }

    [Fact]
    public void Reasons_AreHumanReadableSpanishSentences()
    {
        var snapshot = CreateSnapshot(
            totalRamBytes: 32 * Gib,
            logicalProcessors: 12,
            physicalCores: 6,
            isDedicatedGpu: true,
            dedicatedGraphicsMemoryBytes: 8L * (long)Gib);

        var profile = HardwareCapabilityPolicy.Evaluate(snapshot);

        Assert.Contains(profile.PositiveReasons, r => r.Message == "32 GB de RAM.");
        Assert.Contains(profile.PositiveReasons, r => r.Message == "12 procesadores lógicos.");
        Assert.Contains(profile.PositiveReasons, r => r.Message == "GPU dedicada detectada.");
        Assert.Contains(profile.PositiveReasons, r => r.Message == "8 GB de memoria gráfica dedicada.");
    }

    [Fact]
    public void SameInput_ProducesIdenticalClassification()
    {
        var snapshot = CreateSnapshot(
            totalRamBytes: 16 * Gib,
            logicalProcessors: 8,
            physicalCores: 4,
            isDedicatedGpu: true,
            dedicatedGraphicsMemoryBytes: 2L * (long)Gib);

        var first = HardwareCapabilityPolicy.Evaluate(snapshot);
        var second = HardwareCapabilityPolicy.Evaluate(snapshot);

        Assert.Equal(first.Tier, second.Tier);
        Assert.Equal(first.Summary, second.Summary);
        Assert.Equal(first.OverallConfidence, second.OverallConfidence);
        Assert.True(first.PositiveReasons.SequenceEqual(second.PositiveReasons));
        Assert.True(first.Limitations.SequenceEqual(second.Limitations));
        Assert.True(first.MissingData.SequenceEqual(second.MissingData));
    }

    private static HardwareCapabilitySnapshot CreateUniformSnapshotAtTier(HardwareCapabilityTier tier)
    {
        return tier switch
        {
            HardwareCapabilityTier.Basic => CreateSnapshot(
                totalRamBytes: 4 * Gib,
                logicalProcessors: 4,
                physicalCores: 2,
                isDedicatedGpu: false),
            HardwareCapabilityTier.Standard => CreateSnapshot(
                totalRamBytes: 8 * Gib,
                logicalProcessors: 5,
                physicalCores: 4,
                isDedicatedGpu: true,
                dedicatedGraphicsMemoryBytes: 1L * (long)Gib),
            HardwareCapabilityTier.Accelerated => CreateSnapshot(
                totalRamBytes: 16 * Gib,
                logicalProcessors: 9,
                physicalCores: 8,
                isDedicatedGpu: true,
                dedicatedGraphicsMemoryBytes: 4L * (long)Gib),
            HardwareCapabilityTier.HighPerformance => CreateSnapshot(
                totalRamBytes: 32 * Gib,
                logicalProcessors: 17,
                physicalCores: 16,
                isDedicatedGpu: true,
                dedicatedGraphicsMemoryBytes: 8L * (long)Gib),
            _ => throw new ArgumentOutOfRangeException(nameof(tier))
        };
    }

    private static HardwareCapabilitySnapshot CreateSnapshot(
        ulong? totalRamBytes,
        int? logicalProcessors,
        int? physicalCores,
        bool? isDedicatedGpu,
        long? dedicatedGraphicsMemoryBytes = null,
        bool? hasBattery = null)
    {
        var processor = new ProcessorCapability(
            Name: "Test CPU",
            Vendor: "Test Vendor",
            PhysicalCores: physicalCores,
            LogicalProcessors: logicalProcessors,
            OperatingSystemArchitecture: "X64",
            ProcessArchitecture: "X64");

        var memory = new MemoryCapability(totalRamBytes);

        var graphics = new GraphicsCapability(
            Name: isDedicatedGpu is null ? null : "Test GPU",
            IsDedicated: isDedicatedGpu,
            DedicatedMemoryBytes: dedicatedGraphicsMemoryBytes,
            SharedMemoryBytes: null);

        return new HardwareCapabilitySnapshot(
            processor,
            memory,
            new[] { graphics },
            graphics,
            hasBattery,
            WindowsEdition: "Test Edition",
            WindowsVersion: "10.0",
            CapturedAt: DateTimeOffset.UnixEpoch,
            WasCached: false);
    }
}
