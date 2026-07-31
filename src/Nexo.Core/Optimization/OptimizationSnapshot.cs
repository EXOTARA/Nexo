namespace Nexo.Core.Optimization;

/// <summary>
/// Diseño D8 — el estado del sistema ANTES de aplicar un plan. El roadmap lo marca como requisito
/// de diseño, no como opción: "un cambio de sistema mal revertido puede dejar el equipo en peor
/// estado — el snapshot y la reversión no son opcionales".
/// </summary>
public sealed class OptimizationSnapshot
{
    public DateTimeOffset CapturedAt { get; set; }

    public string Scenario { get; set; } = string.Empty;

    /// <summary>GUID del plan de energía activo antes del cambio, o null si no se pudo leer.</summary>
    public string? PreviousPowerPlanId { get; set; }

    public OptimizationSnapshot Copy() => new()
    {
        CapturedAt = CapturedAt,
        Scenario = Scenario,
        PreviousPowerPlanId = PreviousPowerPlanId
    };
}

public interface IOptimizationSnapshotStore
{
    OptimizationSnapshot? Load();

    void Save(OptimizationSnapshot? snapshot);
}

public sealed record OptimizationApplyResult(bool IsApplied, string Detail, int AppliedChangeCount)
{
    public static OptimizationApplyResult Applied(int count, string detail) => new(true, detail, count);

    public static OptimizationApplyResult Failed(string detail) => new(false, detail, 0);
}
