using Nexo.Core.Hardware;
using Nexo.Core.Optimization;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D8 (Fase 4) — la regla que gobierna esta clase es la del roadmap: cada cambio debe
/// justificarse con una medición real del equipo, y **no es una lista genérica de trucos**. Por eso
/// la mayoría de estas pruebas comprueban que, sin el dato, el cambio NO se propone.
/// </summary>
public sealed class OptimizationPlanBuilderTests
{
    private static HardwareCapabilityProfile Profile(
        bool? hasBattery = null,
        ulong? memoryBytes = null,
        bool? dedicatedGraphics = null)
    {
        var graphics = dedicatedGraphics is null
            ? GraphicsCapability.Unknown
            : new GraphicsCapability("Adaptador de prueba", dedicatedGraphics, null, null);

        var snapshot = new HardwareCapabilitySnapshot(
            ProcessorCapability.Unknown,
            new MemoryCapability(memoryBytes),
            [graphics],
            graphics,
            hasBattery,
            "Windows 11 Pro",
            "10.0.26100",
            DateTimeOffset.Now,
            WasCached: false);

        return new HardwareCapabilityProfile(
            snapshot, HardwareCapabilityTier.Standard, "prueba", [], [], [], HardwareDataConfidence.Known);
    }

    private const ulong Gib = 1024UL * 1024UL * 1024UL;

    // ---------- Sin medición, no hay cambio ----------

    [Fact]
    public void WithNoProfileAtAll_ProposesNothingAndSaysWhy()
    {
        var plan = OptimizationPlanBuilder.Build(OptimizationScenario.Jugar, profile: null);

        Assert.Empty(plan.Changes);
        Assert.NotEmpty(plan.SkippedForMissingData);
    }

    [Fact]
    public void WithoutKnowingIfThereIsABattery_DoesNotTouchThePowerPlan()
    {
        var plan = OptimizationPlanBuilder.Build(OptimizationScenario.Jugar, Profile(hasBattery: null));

        Assert.DoesNotContain(plan.Changes, change => change.Target == OptimizationTarget.PowerPlan);
        Assert.Contains(plan.SkippedForMissingData, reason => reason.Contains("batería"));
    }

    [Fact]
    public void WithoutKnowingTheRam_ProposesNothingAboutMemory()
    {
        var plan = OptimizationPlanBuilder.Build(
            OptimizationScenario.Jugar, Profile(hasBattery: false, memoryBytes: null));

        Assert.DoesNotContain(plan.Changes, change => change.Id == "memory.closeApps");
        Assert.Contains(plan.SkippedForMissingData, reason => reason.Contains("RAM"));
    }

    [Fact]
    public void WithoutKnowingTheGraphicsAdapter_ProposesNothingAboutIt()
    {
        var plan = OptimizationPlanBuilder.Build(
            OptimizationScenario.Jugar, Profile(hasBattery: false, dedicatedGraphics: null));

        Assert.DoesNotContain(plan.Changes, change => change.Id == "graphics.integrated");
        Assert.Contains(plan.SkippedForMissingData, reason => reason.Contains("dedicada"));
    }

    // ---------- El plan depende del hardware real, no del escenario a secas ----------

    [Fact]
    public void Battery_OnADesktopWithoutOne_DoesNotProposeSavingPower()
    {
        var plan = OptimizationPlanBuilder.Build(
            OptimizationScenario.Bateria, Profile(hasBattery: false));

        Assert.DoesNotContain(plan.Changes, change => change.Id == "power.saver");
        Assert.Contains(plan.SkippedForMissingData, reason => reason.Contains("no tiene batería"));
    }

    [Fact]
    public void Battery_OnALaptop_ProposesThePowerSaverPlan()
    {
        var plan = OptimizationPlanBuilder.Build(
            OptimizationScenario.Bateria, Profile(hasBattery: true));

        Assert.Contains(plan.Changes, change => change.Id == "power.saver");
    }

    [Fact]
    public void Gaming_WarnsAboutTheAutonomyCostOnlyOnALaptop()
    {
        var laptop = OptimizationPlanBuilder.Build(OptimizationScenario.Jugar, Profile(hasBattery: true));
        var desktop = OptimizationPlanBuilder.Build(OptimizationScenario.Jugar, Profile(hasBattery: false));

        Assert.Contains(
            "gastará más",
            laptop.Changes.Single(change => change.Id == "power.highPerformance").Justification,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "sin batería",
            desktop.Changes.Single(change => change.Id == "power.highPerformance").Justification,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MemoryThreshold_IsStricterForDemandingScenarios()
    {
        // 12 GB bastan para una videollamada y se quedan cortos para editar video: un umbral único
        // sería justo el "truco genérico" que la fase prohíbe.
        var call = OptimizationPlanBuilder.Build(
            OptimizationScenario.Videollamada, Profile(hasBattery: false, memoryBytes: 12 * Gib));
        var editing = OptimizationPlanBuilder.Build(
            OptimizationScenario.EdicionVideo, Profile(hasBattery: false, memoryBytes: 12 * Gib));

        Assert.DoesNotContain(call.Changes, change => change.Id == "memory.closeApps");
        Assert.Contains(editing.Changes, change => change.Id == "memory.closeApps");
    }

    [Fact]
    public void IntegratedGraphics_GetAdviceButDedicatedOnesDoNot()
    {
        var integrated = OptimizationPlanBuilder.Build(
            OptimizationScenario.Jugar, Profile(hasBattery: false, dedicatedGraphics: false));
        var dedicated = OptimizationPlanBuilder.Build(
            OptimizationScenario.Jugar, Profile(hasBattery: false, dedicatedGraphics: true));

        Assert.Contains(integrated.Changes, change => change.Id == "graphics.integrated");
        Assert.DoesNotContain(dedicated.Changes, change => change.Id == "graphics.integrated");
    }

    // ---------- Consejo vs. cambio aplicable ----------

    [Fact]
    public void AdviceIsNeverCountedAsSomethingKohanaWouldApply()
    {
        var plan = OptimizationPlanBuilder.Build(
            OptimizationScenario.Jugar,
            Profile(hasBattery: false, memoryBytes: 4 * Gib, dedicatedGraphics: false));

        Assert.All(plan.ApplicableChanges, change => Assert.NotEqual(OptimizationTarget.Advice, change.Target));
        Assert.Contains(plan.Changes, change => change.Target == OptimizationTarget.Advice);
    }

    [Fact]
    public void APlanWithOnlyAdvice_NeedsNoSnapshot()
    {
        // Sin batería conocida no hay cambio de plan de energía, así que solo quedan consejos: no
        // hay nada que revertir y por tanto nada que guardar.
        var plan = OptimizationPlanBuilder.Build(
            OptimizationScenario.Jugar, Profile(hasBattery: null, memoryBytes: 4 * Gib));

        Assert.False(plan.RequiresSnapshot);
    }

    [Fact]
    public void APlanThatChangesThePowerPlan_RequiresASnapshot()
    {
        var plan = OptimizationPlanBuilder.Build(OptimizationScenario.General, Profile(hasBattery: false));

        Assert.True(plan.RequiresSnapshot);
    }

    [Fact]
    public void Restore_IsAlwaysAnEmptyPlan_BecauseItUndoesRatherThanProposes()
    {
        var plan = OptimizationPlanBuilder.Build(OptimizationScenario.Restaurar, Profile(hasBattery: true));

        Assert.Empty(plan.Changes);
        Assert.False(plan.RequiresSnapshot);
    }

    [Fact]
    public void EveryProposedChange_CarriesAJustification()
    {
        foreach (var scenario in Enum.GetValues<OptimizationScenario>())
        {
            var plan = OptimizationPlanBuilder.Build(
                scenario, Profile(hasBattery: true, memoryBytes: 4 * Gib, dedicatedGraphics: false));

            Assert.All(plan.Changes, change => Assert.False(string.IsNullOrWhiteSpace(change.Justification)));
        }
    }
}
