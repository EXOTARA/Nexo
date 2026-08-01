namespace Nexo.Core.Optimization;

/// <summary>
/// Diseño D11 — resultado de UN paso. Cada paso responde por sí mismo para que
/// <see cref="OptimizationCoordinator"/> pueda decidir qué hacer con los anteriores cuando uno
/// falla a mitad.
/// </summary>
public sealed record OptimizationStepResult(bool Success, string Detail)
{
    public static OptimizationStepResult Ok(string detail) => new(true, detail);

    public static OptimizationStepResult Failed(string detail) => new(false, detail);
}

/// <summary>
/// Diseño D8 — aplica y revierte los cambios de sistema que Kohana sí ejecuta. Separado del
/// planificador para que el plan pueda calcularse y mostrarse sin ninguna capacidad de tocar el
/// sistema.
///
/// Diseño D11 — la interfaz pasó de "aplica este plan" a "aplica este cambio". El motivo es la
/// reversión: con un solo método que aplicaba el plan entero, un fallo a mitad dejaba el equipo en
/// un estado que nadie había pedido y que nadie sabía describir. Ahora quien orquesta sabe
/// exactamente qué pasos llegaron a aplicarse y puede deshacerlos.
///
/// **Toda implementación debe VERIFICAR releyendo el estado**: devolver éxito porque la llamada no
/// dio error no es lo mismo que comprobar que el cambio ocurrió, y la fase exige reversión
/// verificada, no anunciada.
/// </summary>
public interface IOptimizationApplier
{
    /// <summary>Lee el estado actual para poder volver a él. Null si no se pudo leer.</summary>
    string? ReadCurrentPowerPlanId();

    OptimizationStepResult ApplyPowerPlan(string changeId);

    OptimizationStepResult RestorePowerPlan(string previousPowerPlanId);
}
