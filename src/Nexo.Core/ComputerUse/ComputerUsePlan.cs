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
    /// <summary>
    /// El nivel por omisión NO sube con D18. Que ejecutar sea posible no significa que deba estar
    /// encendido, y ésta es la capacidad más arriesgada del roadmap: subirla es una decisión de la
    /// persona. Además, el permiso de Computer Use llega Bloqueado (D16), así que hacen falta dos
    /// decisiones distintas, no una.
    /// </summary>
    public static AutonomyLevel Default => AutonomyLevel.Proponer;

    public static bool IsAvailable(AutonomyLevel level) => level switch
    {
        AutonomyLevel.Ver or
        AutonomyLevel.Guiar or
        AutonomyLevel.Proponer or
        AutonomyLevel.EjecutarUnPaso => true,
        _ => false
    };

    /// <summary>
    /// **Diseño D18 — el nivel 4 se abre.** Se abre porque existen las tres cosas que el modelo de
    /// confianza pide antes de ejecutar: el Permission Broker (D16), el Audit Log (D13) y una
    /// reversión real para el único método que la admite (el portapapeles guarda lo que había). Los
    /// comandos de la lista no necesitan reversión porque no cambian nada, y eso lo defiende
    /// <see cref="SafeShellCatalog"/>, no una promesa.
    ///
    /// Los niveles 5 y 6 siguen cerrados: encadenar pasos y automatizar una secuencia son problemas
    /// distintos de "hacer una acción confirmada", y ninguno se ha demostrado.
    /// </summary>
    public static bool CanExecute(AutonomyLevel level) =>
        IsAvailable(level) && level >= AutonomyLevel.EjecutarUnPaso;

    public static string ExplainCannotExecute(AutonomyLevel level) =>
        IsAvailable(level)
            ? "Con el nivel actual te digo qué haría y cómo, pero no lo hago. Puedes subirlo a " +
              "«Ejecutar un paso» en Personalizar."
            : "Todavía no encadeno varias acciones seguidas. Hago una cada vez, y cada una la " +
              "confirmas tú.";

    public static string Describe(AutonomyLevel level) => level switch
    {
        AutonomyLevel.Ver => "Ver: describo lo que hay, sin proponer nada.",
        AutonomyLevel.Guiar => "Guiar: te digo qué harías tú, y lo haces tú.",
        AutonomyLevel.Proponer => "Proponer: redacto el plan y el método, sin ejecutarlo.",
        AutonomyLevel.EjecutarUnPaso => "Ejecutar un paso: hago una acción cada vez, y la confirmas tú.",
        _ => "Ese nivel todavía no está disponible para actuar sobre el equipo."
    };
}
