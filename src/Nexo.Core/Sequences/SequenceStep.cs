namespace Nexo.Core.Sequences;

public sealed record SequenceStepResult(bool Success, string Detail)
{
    public static SequenceStepResult Ok(string detail) => new(true, detail);

    public static SequenceStepResult Failed(string detail) => new(false, detail);
}

/// <summary>
/// Diseño D24 (nivel 5) — un paso de una secuencia.
///
/// <see cref="CanRevert"/> no es informativo: el modelo de confianza exige que, si algo falla a
/// mitad, Sakura pueda *"ofrecer revertir lo ya aplicado usando el snapshot previo"*. Un paso que no
/// sabe volver atrás convierte esa promesa en imposible para toda la secuencia que venga después, y
/// eso hay que decirlo antes de empezar, no descubrirlo al fallar.
///
/// <see cref="IsRiskPoint"/> marca los pasos que se confirman **aunque la secuencia ya esté
/// aprobada**. El nivel 5 es literalmente "ejecuta varios pasos, confirmando en los puntos de
/// riesgo": sin puntos de riesgo sería el nivel 6 con otro nombre.
/// </summary>
public sealed record SequenceStep(
    string Id,
    string Title,
    bool IsRiskPoint,
    bool CanRevert,
    Func<SequenceStepResult> Execute,
    Func<SequenceStepResult>? Revert = null);

/// <summary>Diseño D24 — la secuencia completa, tal y como se le enseña a la persona antes de nada.</summary>
public sealed record SequencePlan(string Description, IReadOnlyList<SequenceStep> Steps)
{
    public bool IsFullyReversible => Steps.All(step => step.CanRevert);

    public IReadOnlyList<SequenceStep> RiskPoints => [.. Steps.Where(step => step.IsRiskPoint)];

    /// <summary>
    /// Aviso que acompaña a la confirmación inicial. Una secuencia que no se puede deshacer entera
    /// debe decirlo **antes**, porque después ya no hay nada que decidir.
    /// </summary>
    public string ReversalWarning => IsFullyReversible
        ? "Si algo falla a mitad, puedo dejarlo todo como estaba."
        : "Ojo: hay pasos que no puedo deshacer. Si algo falla después de uno de ésos, no podré " +
          "devolverlo todo a como estaba.";
}
