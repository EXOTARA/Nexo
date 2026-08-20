namespace Nexo.Core.Audit;

/// <summary>
/// Diseño D13 — qué capacidad hizo algo. Sirve para filtrar: "¿qué ha tocado Sakura en mi equipo?"
/// y "¿qué ha hecho con mi proyecto?" son dos preguntas distintas.
/// </summary>
public enum AuditCapability
{
    Optimizacion,

    Proyecto,

    Memoria,

    Permisos
}

/// <summary>
/// Diseño D13 (capa 11 de la arquitectura) — una entrada del Audit Log **orientado al usuario**. El
/// modelo de confianza es literal sobre qué tiene que traer cada una: *"qué se hizo, cuándo, con qué
/// permiso, y cómo revertirlo si aplica"*, y dice también que los logs de diagnóstico por subsistema
/// (`command-center.log` y compañía) **no cumplen este propósito**: son técnicos y están escritos
/// para depurar, no para que alguien entienda qué pasó con su equipo.
///
/// De ahí que los cuatro campos existan como campos y no como texto libre dentro de
/// <see cref="Detail"/>: si "cómo revertirlo" fuera parte de la frase, nada obligaría a rellenarlo, y
/// la primera acción irreversible pasaría desapercibida.
/// </summary>
public sealed class AuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Cuándo.</summary>
    public DateTimeOffset At { get; set; }

    public AuditCapability Capability { get; set; }

    /// <summary>Qué se hizo, en una etiqueta corta ("Aplicado", "Revocado", "Archivo modificado").</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Qué se hizo, en lenguaje de persona.</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>
    /// Con qué permiso. Texto, no enum: los permisos de Sakura no viven todos en el mismo enum
    /// (una carpeta autorizada, la memoria activada, un plan de energía confirmado), y forzarlos a
    /// uno solo obligaría a inventar categorías que no significan nada.
    /// </summary>
    public string Permission { get; set; } = string.Empty;

    /// <summary>
    /// Nivel de autonomía con el que se actuó, cuando aplica. El modelo de confianza exige registro
    /// a partir del nivel 4; registrar también los de abajo no sobra, porque así se ve que una
    /// capacidad **no** subió de nivel.
    /// </summary>
    public int? AutonomyLevel { get; set; }

    /// <summary>Cómo revertirlo, en lenguaje de persona. Vacío cuando no hay vuelta atrás.</summary>
    public string RevertHint { get; set; } = string.Empty;

    /// <summary>
    /// Identificador que la capacidad usa para deshacer (id de checkpoint, por ejemplo). Vacío
    /// cuando la reversión no es automática.
    /// </summary>
    public string RevertToken { get; set; } = string.Empty;

    public bool CanRevert => !string.IsNullOrWhiteSpace(RevertToken);

    public AuditEntry Copy() => new()
    {
        Id = Id,
        At = At,
        Capability = Capability,
        Action = Action,
        Detail = Detail,
        Permission = Permission,
        AutonomyLevel = AutonomyLevel,
        RevertHint = RevertHint,
        RevertToken = RevertToken
    };

    /// <summary>Una línea legible. Si no hay vuelta atrás, se dice; no se calla.</summary>
    public string Describe()
    {
        var reversal = string.IsNullOrWhiteSpace(RevertHint)
            ? "Sin vuelta atrás automática."
            : RevertHint;

        return $"{At:yyyy-MM-dd HH:mm} · {Capability} · {Action}: {Detail} " +
               $"(permiso: {(string.IsNullOrWhiteSpace(Permission) ? "—" : Permission)}. {reversal})";
    }
}

/// <summary>
/// Diseño D13 — de solo añadir. No hay borrado selectivo a propósito: poder quitar una entrada
/// concreta convertiría la auditoría en una versión de los hechos en vez de en un registro. El
/// almacén sí recorta las más viejas por tamaño.
/// </summary>
public interface IAuditLog
{
    IReadOnlyList<AuditEntry> Read();

    void Append(AuditEntry entry);
}

public sealed class AuditState
{
    /// <summary>Historial reciente, no archivo perpetuo.</summary>
    public const int MaximumEntries = 200;

    public List<AuditEntry> Entries { get; set; } = [];

    public AuditState Copy() => new()
    {
        Entries = Entries.Select(entry => entry.Copy()).ToList()
    };
}
