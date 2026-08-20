namespace Nexo.Core.Optimization;

/// <summary>Qué toca un cambio del plan. Determina cómo se guarda y cómo se revierte.</summary>
public enum OptimizationTarget
{
    /// <summary>Plan de energía de Windows. Reversible por completo: se guarda el GUID anterior.</summary>
    PowerPlan,

    /// <summary>
    /// Diseño D11 — el consumo de la propia Sakura (modo de rendimiento de sus motores). Reversible
    /// por completo: vive en el archivo de preferencias, así que deshacerlo es reescribir el valor
    /// anterior.
    /// </summary>
    SakuraFootprint,

    /// <summary>
    /// Consejo que Sakura NO aplica: lo ejecuta la persona. No necesita snapshot porque Sakura no
    /// cambió nada.
    /// </summary>
    Advice
}

/// <summary>
/// Diseño D8 — un cambio propuesto. <see cref="Justification"/> no es decorativa: el roadmap exige
/// que cada cambio se justifique con una medición real del equipo, no con una lista genérica de
/// "trucos" de internet. Si no hay una medición que lo respalde, el cambio no entra en el plan.
/// </summary>
public sealed record OptimizationChange(
    string Id,
    string Title,
    string Justification,
    OptimizationTarget Target,
    bool IsReversibleBySakura);
