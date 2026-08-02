namespace Nexo.Core.Workspace;

/// <summary>
/// Diseño D12 (Fase 5) — qué niveles de autonomía puede ofrecer el acompañante de proyecto.
///
/// El modelo de confianza es explícito: *"ninguna capacidad nueva puede empezar en el nivel 6: cada
/// una debe demostrarse en los niveles 1–3 antes de solicitar el salto a ejecución"*. D12 se quedó
/// en 1–3 y no escribía ni un byte, porque faltaba lo que el mismo modelo exige para escribir.
///
/// **Diseño D14 — el nivel 4 se abre**, y se abre porque las dos condiciones que lo bloqueaban ya se
/// cumplen, no porque haya pasado un sprint: existe el snapshot previo reversible por archivo
/// (<see cref="WorkspaceCheckpoint"/>) y existe el Audit Log orientado al usuario
/// (<c>Nexo.Core.Audit</c>). Los niveles 5 y 6 siguen cerrados: encadenar varios pasos y automatizar
/// una secuencia son problemas distintos de "hacer un cambio confirmado", y ninguno se ha
/// demostrado todavía.
///
/// <see cref="IsAvailable"/> falla cerrado, igual que <c>AmbientPermissionPolicy</c>: un nivel que
/// no se reconozca explícitamente se niega, para que añadir valores al enum no escale la autonomía
/// en silencio.
/// </summary>
public static class WorkspaceAutonomyPolicy
{
    /// <summary>
    /// El nivel por omisión NO sube con D14. Que escribir sea posible no significa que deba estar
    /// encendido: subir a nivel 4 es una decisión de la persona, y una actualización no la toma por
    /// ella.
    /// </summary>
    public static WorkspaceAutonomyLevel Default => WorkspaceAutonomyLevel.Guiar;

    public static bool IsAvailable(WorkspaceAutonomyLevel level) => level switch
    {
        WorkspaceAutonomyLevel.Ver or
        WorkspaceAutonomyLevel.Guiar or
        WorkspaceAutonomyLevel.Proponer or
        WorkspaceAutonomyLevel.EjecutarUnPaso => true,
        _ => false
    };

    /// <summary>
    /// Diseño D14 — si Kohana puede escribir con este nivel. Separado de
    /// <see cref="IsAvailable"/> a propósito: "el nivel se puede elegir" y "con este nivel se toca
    /// el disco" son preguntas distintas, y mezclarlas es como se cuela una escritura donde no
    /// debía.
    /// </summary>
    public static bool CanWrite(WorkspaceAutonomyLevel level) =>
        IsAvailable(level) && level >= WorkspaceAutonomyLevel.EjecutarUnPaso;

    /// <summary>Por qué un nivel no está disponible. Se dice, no se ignora en silencio.</summary>
    public static string ExplainUnavailable(WorkspaceAutonomyLevel level) => level switch
    {
        WorkspaceAutonomyLevel.ColaborarConConfirmaciones or
        WorkspaceAutonomyLevel.AutomatizarSecuencia =>
            "Todavía no puedo encadenar varios cambios seguidos. Hoy hago uno cada vez, y cada uno " +
            "lo confirmas tú.",
        _ => "Ese nivel de autonomía no existe."
    };

    public static string Describe(WorkspaceAutonomyLevel level) => level switch
    {
        WorkspaceAutonomyLevel.Ver => "Ver: leo y describo, sin sugerir cambios.",
        WorkspaceAutonomyLevel.Guiar => "Guiar: te digo qué harías tú, y lo haces tú.",
        WorkspaceAutonomyLevel.Proponer => "Proponer: redacto el plan de un cambio, sin aplicarlo.",
        WorkspaceAutonomyLevel.EjecutarUnPaso =>
            "Ejecutar un paso: aplico un cambio cada vez, y cada uno lo confirmas tú.",
        WorkspaceAutonomyLevel.ColaborarConConfirmaciones => "Colaborar con confirmaciones (no disponible todavía).",
        WorkspaceAutonomyLevel.AutomatizarSecuencia => "Automatizar una secuencia (no disponible todavía).",
        _ => level.ToString()
    };
}
