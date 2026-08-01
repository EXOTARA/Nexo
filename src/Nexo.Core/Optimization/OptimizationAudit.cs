namespace Nexo.Core.Optimization;

/// <summary>
/// Diseño D11 (Fase 4) — qué pasó realmente. El roadmap lista la auditoría entre las tecnologías de
/// esta fase, y con motivo: un cambio de sistema que nadie puede reconstruir después es
/// indistinguible de un cambio que no se hizo.
/// </summary>
public enum OptimizationAuditAction
{
    Aplicado,

    Restaurado,

    /// <summary>Se intentó y no se pudo. Se registra igual: un intento fallido también explica el estado del equipo.</summary>
    Fallido,

    /// <summary>
    /// Un paso falló a mitad y Kohana deshizo lo que ya había aplicado. Es el registro más
    /// importante de los cuatro: significa que el equipo NO se quedó a medias.
    /// </summary>
    RevertidoPorFallo
}

public sealed class OptimizationAuditEntry
{
    public DateTimeOffset At { get; set; }

    public string Scenario { get; set; } = string.Empty;

    public OptimizationAuditAction Action { get; set; }

    /// <summary>Ids de los cambios implicados, en el orden en que se intentaron.</summary>
    public List<string> Changes { get; set; } = [];

    public string Detail { get; set; } = string.Empty;

    public OptimizationAuditEntry Copy() => new()
    {
        At = At,
        Scenario = Scenario,
        Action = Action,
        Changes = [.. Changes],
        Detail = Detail
    };

    public string Describe() =>
        $"{At:yyyy-MM-dd HH:mm} · {Scenario} · {Action}: {Detail}";
}

/// <summary>
/// Diseño D11 — registro de solo-añadir. No hay borrado selectivo a propósito: poder quitar una
/// entrada concreta convertiría la auditoría en una versión de los hechos en vez de en un registro.
/// El almacén sí recorta las más viejas por tamaño.
/// </summary>
public interface IOptimizationAuditLog
{
    IReadOnlyList<OptimizationAuditEntry> Read();

    void Append(OptimizationAuditEntry entry);
}

public sealed class OptimizationAuditState
{
    /// <summary>Tope duro: la auditoría es un historial reciente, no un archivo perpetuo.</summary>
    public const int MaximumEntries = 50;

    public List<OptimizationAuditEntry> Entries { get; set; } = [];

    public OptimizationAuditState Copy() => new()
    {
        Entries = Entries.Select(entry => entry.Copy()).ToList()
    };
}
