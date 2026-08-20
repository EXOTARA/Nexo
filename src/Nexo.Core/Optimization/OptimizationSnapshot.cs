namespace Nexo.Core.Optimization;

/// <summary>
/// Diseño D8 — el estado del sistema ANTES de aplicar un plan. El roadmap lo marca como requisito
/// de diseño, no como opción: "un cambio de sistema mal revertido puede dejar el equipo en peor
/// estado — el snapshot y la reversión no son opcionales".
///
/// Diseño D11 — guarda también el modo de rendimiento propio de Sakura, el segundo objetivo que se
/// aplica de verdad. Cada objetivo que se aplique tiene que traer su valor anterior aquí: un
/// objetivo sin línea de vuelta no debería poder aplicarse.
/// </summary>
public sealed class OptimizationSnapshot
{
    public DateTimeOffset CapturedAt { get; set; }

    public string Scenario { get; set; } = string.Empty;

    /// <summary>GUID del plan de energía activo antes del cambio, o null si no se pudo leer.</summary>
    public string? PreviousPowerPlanId { get; set; }

    /// <summary>
    /// Diseño D11 — modo de rendimiento de Sakura antes del cambio, como texto para que el archivo
    /// siga siendo legible y sobreviva a que el enum crezca.
    /// </summary>
    public string? PreviousSakuraPerformanceMode { get; set; }

    public OptimizationSnapshot Copy() => new()
    {
        CapturedAt = CapturedAt,
        Scenario = Scenario,
        PreviousPowerPlanId = PreviousPowerPlanId,
        PreviousSakuraPerformanceMode = PreviousSakuraPerformanceMode
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
