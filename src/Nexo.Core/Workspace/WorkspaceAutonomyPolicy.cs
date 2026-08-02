namespace Nexo.Core.Workspace;

/// <summary>
/// Diseño D12 (Fase 5) — qué niveles de autonomía puede ofrecer HOY el acompañante de proyecto.
///
/// El modelo de confianza es explícito: *"ninguna capacidad nueva puede empezar en el nivel 6: cada
/// una debe demostrarse en los niveles 1–3 antes de solicitar el salto a ejecución"*. D12 es el
/// primer sprint de esta capacidad, así que se queda en 1–3: **no escribe ni un byte en el
/// proyecto**. El paso a ejecución (nivel 4 en adelante) exige además lo que el mismo modelo pide y
/// aquí todavía no existe: snapshot previo reversible por archivo y el Audit Log orientado al
/// usuario.
///
/// <see cref="IsAvailable"/> falla cerrado, igual que <c>AmbientPermissionPolicy</c>: un nivel que
/// no se reconozca explícitamente se niega, para que añadir valores al enum no escale la autonomía
/// en silencio.
/// </summary>
public static class WorkspaceAutonomyPolicy
{
    public static WorkspaceAutonomyLevel Default => WorkspaceAutonomyLevel.Guiar;

    public static bool IsAvailable(WorkspaceAutonomyLevel level) => level switch
    {
        WorkspaceAutonomyLevel.Ver or WorkspaceAutonomyLevel.Guiar or WorkspaceAutonomyLevel.Proponer =>
            true,
        _ => false
    };

    /// <summary>Por qué un nivel no está disponible. Se dice, no se ignora en silencio.</summary>
    public static string ExplainUnavailable(WorkspaceAutonomyLevel level) => level switch
    {
        WorkspaceAutonomyLevel.EjecutarUnPaso or
        WorkspaceAutonomyLevel.ColaborarConConfirmaciones or
        WorkspaceAutonomyLevel.AutomatizarSecuencia =>
            "Todavía no puedo modificar archivos de tu proyecto. Antes tengo que poder guardar una " +
            "copia previa de cada archivo y registrar cada cambio para que puedas deshacerlo.",
        _ => "Ese nivel de autonomía no existe."
    };

    public static string Describe(WorkspaceAutonomyLevel level) => level switch
    {
        WorkspaceAutonomyLevel.Ver => "Ver: leo y describo, sin sugerir cambios.",
        WorkspaceAutonomyLevel.Guiar => "Guiar: te digo qué harías tú, y lo haces tú.",
        WorkspaceAutonomyLevel.Proponer => "Proponer: redacto el plan de un cambio, sin aplicarlo.",
        WorkspaceAutonomyLevel.EjecutarUnPaso => "Ejecutar un paso (no disponible todavía).",
        WorkspaceAutonomyLevel.ColaborarConConfirmaciones => "Colaborar con confirmaciones (no disponible todavía).",
        WorkspaceAutonomyLevel.AutomatizarSecuencia => "Automatizar una secuencia (no disponible todavía).",
        _ => level.ToString()
    };
}
