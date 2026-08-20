namespace Nexo.Core.Optimization;

/// <summary>
/// Diseño D8 — el plan que se le enseña a la persona ANTES de tocar nada. Nunca se aplica sin
/// confirmación explícita: cambiar la configuración del sistema es, en el modelo de confianza, una
/// acción de riesgo alto con snapshot previo obligatorio.
/// </summary>
public sealed record OptimizationPlan(
    OptimizationScenario Scenario,
    string Summary,
    IReadOnlyList<OptimizationChange> Changes,
    IReadOnlyList<string> SkippedForMissingData)
{
    /// <summary>Cambios que Sakura aplicaría de verdad (los demás son consejos para la persona).</summary>
    public IReadOnlyList<OptimizationChange> ApplicableChanges =>
        [.. Changes.Where(change => change.Target != OptimizationTarget.Advice)];

    public bool RequiresSnapshot => ApplicableChanges.Count > 0;
}
