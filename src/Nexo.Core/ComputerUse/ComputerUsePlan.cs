using Nexo.Core.Permissions;

namespace Nexo.Core.ComputerUse;

/// <summary>Diseño D17 — lo que la persona quiere que pase en su equipo.</summary>
public sealed record ComputerUseIntent(
    string Description,
    string? TargetApp = null,
    IReadOnlyList<MandatoryConfirmation>? Categories = null)
{
    public IReadOnlyList<MandatoryConfirmation> MandatoryCategories => Categories ?? [];
}

/// <summary>
/// Diseño D17 (Fase 7) — el plan: qué se haría, por qué método, por qué ése y qué se descartó. Es lo
/// único que produce la fase en los niveles 1–3; ejecutarlo es otra decisión y otro nivel.
/// </summary>
public sealed record ComputerUsePlan(
    ComputerUseIntent Intent,
    ComputerUseMethodChoice Choice,
    AutonomyLevel Level,
    bool IsReversible,
    string ReversalNote,
    string? Blocker)
{
    public bool CanBeExecuted => Blocker is null && Choice.HasMethod;
}

/// <summary>
/// Diseño D17 — arma el plan. **No ejecuta nada**: en los niveles 1–3 del modelo de confianza Kohana
/// observa, guía y propone, y la Fase 7 es explícita en que *"no se salta niveles del modelo de
/// autonomía"*.
///
/// El plan se arma aunque falte permiso o método, y en ese caso trae el motivo en
/// <see cref="ComputerUsePlan.Blocker"/> en vez de no existir. Un plan que no llega a formarse deja
/// a la persona sin saber qué haría falta para que sí; uno que se forma y explica qué lo bloquea es
/// exactamente lo que "proponer" significa.
/// </summary>
public static class ComputerUsePlanner
{
    public static ComputerUsePlan Build(
        ComputerUseIntent intent,
        IEnumerable<ComputerUseMethod> availableMethods,
        PermissionSettings permissions,
        AutonomyLevel level,
        bool simulatedInputAllowed = false)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(permissions);

        var choice = ComputerUseMethodPolicy.Choose(availableMethods, simulatedInputAllowed);

        var decision = PermissionBroker.Decide(
            new PermissionRequest(
                KohanaCapability.ComputerUse,
                intent.Description,
                intent.TargetApp,
                intent.MandatoryCategories),
            permissions);

        string? blocker = null;
        if (decision.IsDenied)
        {
            blocker = decision.Reason;
        }
        else if (!choice.HasMethod)
        {
            blocker = choice.Reason;
        }
        else if (!ComputerUseAutonomyPolicy.CanExecute(level))
        {
            blocker = ComputerUseAutonomyPolicy.ExplainCannotExecute(level);
        }

        var reversible = IsReversible(choice.Method);

        return new ComputerUsePlan(
            intent,
            choice,
            level,
            reversible,
            reversible
                ? "Puedo dejarlo como estaba."
                : "No puedo deshacerlo yo: hazlo tú si hace falta.",
            blocker);
    }

    /// <summary>
    /// Si Kohana puede deshacer lo que ese método hace. Solo se marca reversible lo que se puede
    /// devolver **con certeza**: el portapapeles porque se guarda lo que había, y un comando de solo
    /// lectura porque no cambió nada. Todo lo demás se declara irreversible aunque a veces pudiera
    /// deshacerse, porque "a veces" no es una garantía que se pueda ofrecer.
    /// </summary>
    private static bool IsReversible(ComputerUseMethod? method) => method switch
    {
        ComputerUseMethod.Portapapeles => true,
        ComputerUseMethod.ShellSeguro => true,
        _ => false
    };
}

/// <summary>
/// Diseño D17 — qué niveles ofrece Computer Use. Misma forma que
/// <c>WorkspaceAutonomyPolicy</c> y por el mismo motivo: el modelo de confianza exige que cada
/// capacidad se demuestre en los niveles 1–3 antes de pedir ejecución, y esta es la más arriesgada
/// del roadmap.
///
/// Falla cerrado ante cualquier nivel que no reconozca.
/// </summary>
public static class ComputerUseAutonomyPolicy
{
    public static AutonomyLevel Default => AutonomyLevel.Proponer;

    public static bool IsAvailable(AutonomyLevel level) => level switch
    {
        AutonomyLevel.Ver or AutonomyLevel.Guiar or AutonomyLevel.Proponer => true,
        _ => false
    };

    /// <summary>
    /// Diseño D17 — hoy **nadie** puede ejecutar: la capacidad acaba de nacer y el modelo de
    /// confianza prohíbe empezar por arriba. D18 abre el nivel 4 cuando existan la ejecución
    /// verificada y su reversión.
    /// </summary>
    public static bool CanExecute(AutonomyLevel level) => false;

    public static string ExplainCannotExecute(AutonomyLevel level) =>
        "Todavía no ejecuto acciones sobre el equipo: de momento te digo qué haría y cómo, y lo " +
        "haces tú.";

    public static string Describe(AutonomyLevel level) => level switch
    {
        AutonomyLevel.Ver => "Ver: describo lo que hay, sin proponer nada.",
        AutonomyLevel.Guiar => "Guiar: te digo qué harías tú, y lo haces tú.",
        AutonomyLevel.Proponer => "Proponer: redacto el plan y el método, sin ejecutarlo.",
        _ => "Ese nivel todavía no está disponible para actuar sobre el equipo."
    };
}
