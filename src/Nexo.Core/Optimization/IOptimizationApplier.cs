namespace Nexo.Core.Optimization;

/// <summary>
/// Diseño D8 — aplica y revierte los cambios que Kohana sí ejecuta. Separado del planificador para
/// que el plan pueda calcularse y mostrarse sin ninguna capacidad de tocar el sistema.
/// </summary>
public interface IOptimizationApplier
{
    /// <summary>Lee el estado actual para poder volver a él. Null si no se pudo leer.</summary>
    string? ReadCurrentPowerPlanId();

    OptimizationApplyResult Apply(OptimizationPlan plan);

    OptimizationApplyResult Restore(OptimizationSnapshot snapshot);
}
