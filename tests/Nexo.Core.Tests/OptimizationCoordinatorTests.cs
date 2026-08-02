using Nexo.Core.AdaptiveEngine;
using Nexo.Core.Audit;
using Nexo.Core.Optimization;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D11 (Fase 4) — el criterio de terminado de la fase pide "aplicación y reversión completa
/// verificada". Lo que se prueba aquí es exactamente eso: que no se toca nada sin línea de vuelta,
/// que el snapshot llega al disco antes que el primer cambio, y que un fallo a mitad devuelve el
/// equipo a como estaba en vez de dejarlo a medias.
/// </summary>
public sealed class OptimizationCoordinatorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 1, 10, 0, 0, TimeSpan.FromHours(-6));

    private const string PreviousPlan = "381b4222-f694-41f0-9685-ff5bb260df2e";

    // ---------- Dobles ----------

    private sealed class FakeSystemApplier : IOptimizationApplier
    {
        public string? CurrentPowerPlanId { get; set; } = PreviousPlan;

        public bool FailApply { get; set; }

        public bool FailRestore { get; set; }

        public List<string> Applied { get; } = [];

        public List<string> Restored { get; } = [];

        public string? ReadCurrentPowerPlanId() => CurrentPowerPlanId;

        public OptimizationStepResult ApplyPowerPlan(string changeId)
        {
            if (FailApply)
            {
                return OptimizationStepResult.Failed("Windows no aceptó el cambio de plan de energía.");
            }

            Applied.Add(changeId);
            return OptimizationStepResult.Ok("Plan de energía verificado.");
        }

        public OptimizationStepResult RestorePowerPlan(string previousPowerPlanId)
        {
            if (FailRestore)
            {
                return OptimizationStepResult.Failed("Windows no aceptó restaurar el plan anterior.");
            }

            Restored.Add(previousPowerPlanId);
            return OptimizationStepResult.Ok("Restaurado.");
        }
    }

    private sealed class FakeFootprintApplier : IKohanaFootprintApplier
    {
        public HardwarePerformanceMode Mode { get; set; } = HardwarePerformanceMode.Balanced;

        public bool FailApply { get; set; }

        public List<HardwarePerformanceMode> Applied { get; } = [];

        public HardwarePerformanceMode ReadCurrentMode() => Mode;

        public OptimizationStepResult ApplyMode(HardwarePerformanceMode mode)
        {
            if (FailApply)
            {
                return OptimizationStepResult.Failed("No se pudo guardar el modo.");
            }

            Mode = mode;
            Applied.Add(mode);
            return OptimizationStepResult.Ok($"Kohana quedó en modo {mode}.");
        }
    }

    private sealed class FakeSnapshotStore : IOptimizationSnapshotStore
    {
        public OptimizationSnapshot? Current { get; private set; }

        public int Writes { get; private set; }

        public OptimizationSnapshot? Load() => Current?.Copy();

        public void Save(OptimizationSnapshot? snapshot)
        {
            Current = snapshot?.Copy();
            Writes++;
        }
    }

    private sealed class FakeAuditLog : IAuditLog
    {
        public List<AuditEntry> Entries { get; } = [];

        public IReadOnlyList<AuditEntry> Read() => Entries;

        public void Append(AuditEntry entry) => Entries.Add(entry);
    }

    // ---------- Ayudas ----------

    private static OptimizationChange PowerChange() => new(
        "power.highPerformance",
        "Alto rendimiento",
        "Justificado por el hardware.",
        OptimizationTarget.PowerPlan,
        IsReversibleByKohana: true);

    private static OptimizationChange FootprintChange() => new(
        KohanaFootprintModes.Eco,
        "Kohana en bajo consumo",
        "Justificado por el hardware.",
        OptimizationTarget.KohanaFootprint,
        IsReversibleByKohana: true);

    private static OptimizationChange AdviceChange() => new(
        "memory.closeApps",
        "Cerrar aplicaciones",
        "Justificado por el hardware.",
        OptimizationTarget.Advice,
        IsReversibleByKohana: false);

    private static OptimizationPlan Plan(params OptimizationChange[] changes) =>
        new(OptimizationScenario.Jugar, "Plan de prueba", changes, []);

    private static (OptimizationCoordinator Coordinator,
        FakeSystemApplier System,
        FakeFootprintApplier Footprint,
        FakeSnapshotStore Snapshots,
        FakeAuditLog Audit) Build()
    {
        var system = new FakeSystemApplier();
        var footprint = new FakeFootprintApplier();
        var snapshots = new FakeSnapshotStore();
        var audit = new FakeAuditLog();
        var coordinator = new OptimizationCoordinator(system, footprint, snapshots, audit, () => Now);

        return (coordinator, system, footprint, snapshots, audit);
    }

    // ---------- Sin línea de vuelta no se toca nada ----------

    [Fact]
    public void WithoutAReadableCurrentPowerPlan_NothingIsApplied()
    {
        var (coordinator, system, _, snapshots, audit) = Build();
        system.CurrentPowerPlanId = null;

        var result = coordinator.Apply(Plan(PowerChange()));

        Assert.False(result.IsApplied);
        Assert.Empty(system.Applied);
        Assert.Null(snapshots.Current);
        Assert.StartsWith(nameof(OptimizationAuditAction.Fallido), audit.Entries.Single().Action);
    }

    [Fact]
    public void APlanWithOnlyAdvice_HasNothingToApply()
    {
        var (coordinator, system, _, snapshots, _) = Build();

        var result = coordinator.Apply(Plan(AdviceChange()));

        Assert.False(result.IsApplied);
        Assert.Empty(system.Applied);
        Assert.Null(snapshots.Current);
    }

    // ---------- El snapshot va primero ----------

    [Fact]
    public void TheSnapshotIsWritten_BeforeAnythingIsTouched()
    {
        var system = new FakeSystemApplier();
        var snapshots = new FakeSnapshotStore();
        var writesWhenApplied = -1;

        var recordingSystem = new RecordingSystemApplier(system, () => writesWhenApplied = snapshots.Writes);
        var coordinator = new OptimizationCoordinator(
            recordingSystem, new FakeFootprintApplier(), snapshots, new FakeAuditLog(), () => Now);

        coordinator.Apply(Plan(PowerChange()));

        // Si algo falla a media aplicación, la vuelta atrás ya tiene que estar en disco.
        Assert.Equal(1, writesWhenApplied);
    }

    private sealed class RecordingSystemApplier(FakeSystemApplier inner, Action onApply) : IOptimizationApplier
    {
        public string? ReadCurrentPowerPlanId() => inner.ReadCurrentPowerPlanId();

        public OptimizationStepResult ApplyPowerPlan(string changeId)
        {
            onApply();
            return inner.ApplyPowerPlan(changeId);
        }

        public OptimizationStepResult RestorePowerPlan(string previousPowerPlanId) =>
            inner.RestorePowerPlan(previousPowerPlanId);
    }

    [Fact]
    public void TheSnapshotRemembersEveryTargetThePlanTouches()
    {
        var (coordinator, _, footprint, snapshots, _) = Build();
        footprint.Mode = HardwarePerformanceMode.Maximum;

        coordinator.Apply(Plan(PowerChange(), FootprintChange()));

        Assert.Equal(PreviousPlan, snapshots.Current!.PreviousPowerPlanId);
        Assert.Equal("Maximum", snapshots.Current.PreviousKohanaPerformanceMode);
    }

    // ---------- Fallo a mitad ----------

    [Fact]
    public void WhenAStepFails_TheAlreadyAppliedOnesAreUndone()
    {
        var (coordinator, system, footprint, snapshots, audit) = Build();
        footprint.Mode = HardwarePerformanceMode.Maximum;
        footprint.FailApply = true;

        // El plan de energía se aplica primero y el consumo de Kohana falla después.
        var result = coordinator.Apply(Plan(PowerChange(), FootprintChange()));

        Assert.False(result.IsApplied);
        Assert.Equal([PreviousPlan], system.Restored);
        Assert.Contains("como estaba", result.Detail);
        Assert.StartsWith(nameof(OptimizationAuditAction.RevertidoPorFallo), audit.Entries.Single().Action);

        // Sin snapshot: no quedó nada aplicado que deshacer.
        Assert.Null(snapshots.Current);
        Assert.False(coordinator.HasSomethingToUndo);
    }

    [Fact]
    public void WhenTheRollbackAlsoFails_ItIsSaidOutLoud()
    {
        var (coordinator, system, footprint, _, audit) = Build();
        footprint.FailApply = true;
        system.FailRestore = true;

        var result = coordinator.Apply(Plan(PowerChange(), FootprintChange()));

        Assert.False(result.IsApplied);
        Assert.Contains("no pude deshacer", result.Detail);
        Assert.StartsWith(nameof(OptimizationAuditAction.Fallido), audit.Entries.Single().Action);
    }

    // ---------- Camino feliz y deshacer ----------

    [Fact]
    public void ASuccessfulPlan_AppliesEveryChange_AndLeavesSomethingToUndo()
    {
        var (coordinator, system, footprint, _, audit) = Build();

        var result = coordinator.Apply(Plan(PowerChange(), FootprintChange(), AdviceChange()));

        Assert.True(result.IsApplied);

        // El consejo NO cuenta: lo ejecuta la persona, Kohana no lo aplicó.
        Assert.Equal(2, result.AppliedChangeCount);
        Assert.Equal(["power.highPerformance"], system.Applied);
        Assert.Equal([HardwarePerformanceMode.Eco], footprint.Applied);
        Assert.True(coordinator.HasSomethingToUndo);
        Assert.StartsWith(nameof(OptimizationAuditAction.Aplicado), audit.Entries.Single().Action);
    }

    [Fact]
    public void Undo_ReturnsEveryTargetToItsPreviousValue()
    {
        var (coordinator, system, footprint, _, audit) = Build();
        footprint.Mode = HardwarePerformanceMode.Maximum;

        coordinator.Apply(Plan(PowerChange(), FootprintChange()));
        var result = coordinator.Restore();

        Assert.True(result.IsApplied);
        Assert.Equal([PreviousPlan], system.Restored);
        Assert.Equal(HardwarePerformanceMode.Maximum, footprint.Mode);
        Assert.False(coordinator.HasSomethingToUndo);
        Assert.Contains(audit.Entries, entry => entry.Action.StartsWith(nameof(OptimizationAuditAction.Restaurado)));
    }

    [Fact]
    public void WhenUndoFails_TheSnapshotIsKept_SoItCanBeTriedAgain()
    {
        var (coordinator, system, _, snapshots, _) = Build();

        coordinator.Apply(Plan(PowerChange()));
        system.FailRestore = true;

        var result = coordinator.Restore();

        Assert.False(result.IsApplied);
        Assert.NotNull(snapshots.Current);
        Assert.True(coordinator.HasSomethingToUndo);
    }

    [Fact]
    public void WithNothingApplied_ThereIsNothingToUndo()
    {
        var (coordinator, _, _, _, _) = Build();

        var result = coordinator.Restore();

        Assert.False(result.IsApplied);
        Assert.Contains("No hay ninguna optimización", result.Detail);
    }

    [Fact]
    public void AnUnknownChangeId_IsNotApplied()
    {
        var (coordinator, _, _, _, _) = Build();
        var unknown = new OptimizationChange(
            "kohana.inventado",
            "Cambio inventado",
            "Sin justificación real.",
            OptimizationTarget.KohanaFootprint,
            IsReversibleByKohana: true);

        var result = coordinator.Apply(Plan(unknown));

        Assert.False(result.IsApplied);
        Assert.Contains("No reconozco", result.Detail);
    }
}
