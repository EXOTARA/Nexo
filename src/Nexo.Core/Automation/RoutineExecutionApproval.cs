namespace Nexo.Core.Automation;

/// <summary>
/// Evidencia de que el usuario aprobó **esta ejecución concreta** de una rutina.
///
/// Se pasa como argumento en cada llamada, nunca se guarda en la rutina ni en la acción.
/// Así, aprobar una rutina al crearla no concede permiso permanente para ejecutar comandos
/// arbitrarios, y el contenido que llegue del modelo, OCR, archivos, web o skills no puede
/// fabricar autoridad de usuario incrustándola en los datos de la acción
/// (`SECURITY_MODEL` §Defensa por capas, puntos 2 y 4).
/// </summary>
public enum RoutineExecutionApproval
{
    /// <summary>
    /// No hay confirmación del usuario para esta ejecución. Los pasos sensibles **no** se
    /// ejecutan. Es el valor por defecto: mínimo privilegio.
    /// </summary>
    NotConfirmed,

    /// <summary>
    /// El usuario aprobó explícitamente esta ejecución tras ver el plan completo.
    /// </summary>
    ConfirmedByUser
}
