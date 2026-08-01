using Nexo.Core.AdaptiveEngine;

namespace Nexo.Core.Optimization;

/// <summary>
/// Diseño D11 (Fase 4) — orquesta aplicar y deshacer. En D8 esta lógica vivía en la ventana
/// principal y solo tenía un objetivo que aplicar; con dos, el orden y los fallos parciales dejan de
/// ser un detalle y pasan a ser el problema central, así que vive aquí, en Core, donde se puede
/// probar de verdad con dobles.
///
/// El criterio de terminado de la fase pide los siete escenarios "con plan, confirmación, aplicación
/// y **reversión completa verificada**". Las tres reglas que lo sostienen:
///
/// 1. **Sin línea de vuelta no se aplica nada.** Si no se puede leer el estado anterior de algún
///    objetivo que el plan quiere tocar, se aborta ANTES de tocar nada. No es prudencia de más: sin
///    ese valor, deshacer no existiría.
/// 2. **El snapshot se escribe en disco antes del primer cambio.** Si el proceso muere a mitad, la
///    vuelta atrás ya está guardada. Al revés no habría manera.
/// 3. **Si un paso falla, se deshacen los anteriores.** Un plan a medias deja el equipo en un
///    estado que nadie pidió y que nadie sabría describir — justo el riesgo que el roadmap señala
///    para esta fase.
/// </summary>
public sealed class OptimizationCoordinator
{
    private readonly IOptimizationApplier _system;
    private readonly IKohanaFootprintApplier _footprint;
    private readonly IOptimizationSnapshotStore _snapshots;
    private readonly IOptimizationAuditLog _audit;
    private readonly Func<DateTimeOffset> _clock;

    public OptimizationCoordinator(
        IOptimizationApplier system,
        IKohanaFootprintApplier footprint,
        IOptimizationSnapshotStore snapshots,
        IOptimizationAuditLog audit,
        Func<DateTimeOffset>? clock = null)
    {
        _system = system;
        _footprint = footprint;
        _snapshots = snapshots;
        _audit = audit;
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public bool HasSomethingToUndo => _snapshots.Load() is not null;

    public OptimizationApplyResult Apply(OptimizationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var changes = plan.ApplicableChanges;
        if (changes.Count == 0)
        {
            return OptimizationApplyResult.Failed("Este plan no tiene cambios que Kohana pueda aplicar.");
        }

        var snapshot = CaptureSnapshot(plan, changes, out var blocker);
        if (snapshot is null)
        {
            Record(plan.Scenario, OptimizationAuditAction.Fallido, changes, blocker!);
            return OptimizationApplyResult.Failed(blocker!);
        }

        // Regla 2: primero el disco, después el sistema.
        _snapshots.Save(snapshot);

        var applied = new List<OptimizationChange>();
        foreach (var change in changes)
        {
            var step = ApplyChange(change);
            if (step.Success)
            {
                applied.Add(change);
                continue;
            }

            // Regla 3. Se informa del fallo original, no del rescate: la persona necesita saber qué
            // no se pudo hacer, y que su equipo quedó como estaba.
            var rolledBack = RollBack(snapshot, applied);
            _snapshots.Save(null);

            var detail = rolledBack
                ? $"{step.Detail} Dejé el equipo como estaba antes."
                : $"{step.Detail} Además, no pude deshacer del todo lo ya aplicado: " +
                  "revisa el plan de energía y el modo de rendimiento de Kohana.";

            Record(
                plan.Scenario,
                rolledBack ? OptimizationAuditAction.RevertidoPorFallo : OptimizationAuditAction.Fallido,
                changes,
                detail);

            return OptimizationApplyResult.Failed(detail);
        }

        Record(plan.Scenario, OptimizationAuditAction.Aplicado, applied, "Listo. Puedes deshacerlo cuando quieras.");
        return OptimizationApplyResult.Applied(applied.Count, "Listo. Puedes deshacerlo cuando quieras.");
    }

    public OptimizationApplyResult Restore()
    {
        var snapshot = _snapshots.Load();
        if (snapshot is null)
        {
            return OptimizationApplyResult.Failed("No hay ninguna optimización aplicada que deshacer.");
        }

        var restored = 0;
        var failures = new List<string>();

        if (snapshot.PreviousPowerPlanId is { Length: > 0 } powerPlan)
        {
            var step = _system.RestorePowerPlan(powerPlan);
            if (step.Success)
            {
                restored++;
            }
            else
            {
                failures.Add(step.Detail);
            }
        }

        if (TryParseMode(snapshot.PreviousKohanaPerformanceMode, out var mode))
        {
            var step = _footprint.ApplyMode(mode);
            if (step.Success)
            {
                restored++;
            }
            else
            {
                failures.Add(step.Detail);
            }
        }

        if (failures.Count > 0)
        {
            // El snapshot NO se borra: si algo no se pudo devolver, la línea de vuelta tiene que
            // seguir existiendo para poder intentarlo otra vez.
            var detail = string.Join(" ", failures);
            Record(snapshot.Scenario, OptimizationAuditAction.Fallido, [], detail);
            return OptimizationApplyResult.Failed(detail);
        }

        _snapshots.Save(null);
        var message = restored == 0
            ? "No había nada que devolver."
            : "Devolví el equipo a como estaba.";

        Record(snapshot.Scenario, OptimizationAuditAction.Restaurado, [], message);
        return OptimizationApplyResult.Applied(restored, message);
    }

    public IReadOnlyList<OptimizationAuditEntry> ReadAudit() => _audit.Read();

    /// <summary>
    /// Regla 1: captura el estado anterior de CADA objetivo que el plan piensa tocar. Devuelve null
    /// —con el motivo— en cuanto uno no se pueda leer.
    /// </summary>
    private OptimizationSnapshot? CaptureSnapshot(
        OptimizationPlan plan,
        IReadOnlyList<OptimizationChange> changes,
        out string? blocker)
    {
        blocker = null;
        var snapshot = new OptimizationSnapshot
        {
            CapturedAt = _clock(),
            Scenario = plan.Scenario.ToString()
        };

        if (changes.Any(change => change.Target == OptimizationTarget.PowerPlan))
        {
            snapshot.PreviousPowerPlanId = _system.ReadCurrentPowerPlanId();
            if (snapshot.PreviousPowerPlanId is null)
            {
                blocker = "No pude leer el plan de energía actual, así que no podría deshacerlo después. No cambié nada.";
                return null;
            }
        }

        if (changes.Any(change => change.Target == OptimizationTarget.KohanaFootprint))
        {
            snapshot.PreviousKohanaPerformanceMode = _footprint.ReadCurrentMode().ToString();
        }

        return snapshot;
    }

    private OptimizationStepResult ApplyChange(OptimizationChange change) => change.Target switch
    {
        OptimizationTarget.PowerPlan => _system.ApplyPowerPlan(change.Id),
        OptimizationTarget.KohanaFootprint when KohanaFootprintModes.Resolve(change.Id) is { } mode =>
            _footprint.ApplyMode(mode),
        _ => OptimizationStepResult.Failed($"No reconozco el cambio «{change.Id}», así que no lo apliqué.")
    };

    private bool RollBack(OptimizationSnapshot snapshot, IReadOnlyList<OptimizationChange> applied)
    {
        var allBack = true;

        foreach (var change in applied)
        {
            var step = change.Target switch
            {
                OptimizationTarget.PowerPlan when snapshot.PreviousPowerPlanId is { Length: > 0 } previous =>
                    _system.RestorePowerPlan(previous),
                OptimizationTarget.KohanaFootprint when TryParseMode(snapshot.PreviousKohanaPerformanceMode, out var mode) =>
                    _footprint.ApplyMode(mode),
                _ => OptimizationStepResult.Failed("Sin estado anterior guardado.")
            };

            allBack &= step.Success;
        }

        return allBack;
    }

    private void Record(
        OptimizationScenario scenario,
        OptimizationAuditAction action,
        IReadOnlyList<OptimizationChange> changes,
        string detail) =>
        Record(scenario.ToString(), action, changes, detail);

    private void Record(
        string scenario,
        OptimizationAuditAction action,
        IReadOnlyList<OptimizationChange> changes,
        string detail) =>
        _audit.Append(new OptimizationAuditEntry
        {
            At = _clock(),
            Scenario = scenario,
            Action = action,
            Changes = [.. changes.Select(change => change.Id)],
            Detail = detail
        });

    private static bool TryParseMode(string? value, out HardwarePerformanceMode mode) =>
        Enum.TryParse(value, out mode) && Enum.IsDefined(mode);
}
