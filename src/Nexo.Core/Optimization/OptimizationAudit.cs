namespace Nexo.Core.Optimization;

/// <summary>
/// Diseño D11 (Fase 4) — qué pasó realmente con una optimización.
///
/// Diseño D13 — el almacén propio de esta capacidad desapareció. Ahora escribe en el Audit Log
/// único (<see cref="Nexo.Core.Audit.IAuditLog"/>): un registro por capacidad obliga a la persona a
/// saber de antemano en cuál mirar, y el modelo de confianza pide un solo sitio donde ver qué ha
/// hecho Kohana. Lo que sobrevive aquí es el vocabulario de acciones, que sí es propio de la fase.
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
