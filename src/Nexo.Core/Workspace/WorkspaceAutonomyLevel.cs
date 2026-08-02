namespace Nexo.Core.Workspace;

/// <summary>
/// Diseño D12 (Fase 5 — Project Companion) — los seis niveles de autonomía del modelo de confianza
/// (docs/security/KOHANA_TRUST_AND_AUTONOMY_MODEL.md), completos y numerados como allí.
///
/// El enum los declara todos a propósito, aunque D12 solo ofrezca los tres primeros: un enum
/// recortado obligaría a renumerar cuando lleguen los demás, y renumerar niveles de autonomía es la
/// clase de cambio en la que un archivo de ajustes viejo pasa a significar otra cosa. Quién puede
/// usarse lo decide <see cref="WorkspaceAutonomyPolicy"/>, no la existencia del valor.
/// </summary>
public enum WorkspaceAutonomyLevel
{
    /// <summary>Kohana observa y describe, sin proponer acción.</summary>
    Ver = 1,

    /// <summary>Kohana señala qué podría hacerse; lo ejecuta la persona.</summary>
    Guiar = 2,

    /// <summary>Kohana redacta el plan de una acción concreta, sin ejecutarla.</summary>
    Proponer = 3,

    /// <summary>Ejecuta un único paso confirmado explícitamente. **No disponible en D12.**</summary>
    EjecutarUnPaso = 4,

    /// <summary>Ejecuta varios pasos, confirmando en los puntos de riesgo. **No disponible en D12.**</summary>
    ColaborarConConfirmaciones = 5,

    /// <summary>Ejecuta una secuencia aprobada de antemano. **No disponible en D12.**</summary>
    AutomatizarSecuencia = 6
}
