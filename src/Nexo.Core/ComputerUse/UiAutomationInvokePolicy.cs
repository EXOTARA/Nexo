using Nexo.Core.Vision;

namespace Nexo.Core.ComputerUse;

public enum InvokeRefusal
{
    Permitido,

    /// <summary>Ningún control con ese nombre. No se "aproxima": no hay nada que invocar.</summary>
    NoEncontrado,

    /// <summary>Varios controles coinciden. Kohana no adivina cuál.</summary>
    Ambiguo,

    /// <summary>El control existe pero no es de los que se pulsan.</summary>
    NoInvocable,

    /// <summary>La ventana está marcada como sensible.</summary>
    VentanaSensible,

    /// <summary>No se pudo leer la ventana.</summary>
    SinLectura
}

public sealed record InvokeVerdict(
    InvokeRefusal Refusal,
    string Message,
    UiAutomationElement? Target)
{
    public bool IsAllowed => Refusal == InvokeRefusal.Permitido;

    public static InvokeVerdict Allowed(UiAutomationElement target) =>
        new(InvokeRefusal.Permitido, $"Pulsaría «{target.Name}».", target);

    public static InvokeVerdict Refused(InvokeRefusal refusal, string message) =>
        new(refusal, message, null);
}

/// <summary>
/// Diseño D19 (Fase 7, escalón 5) — decide si un control concreto puede invocarse. Es la pieza que
/// convierte "UI Automation" de algo que Kohana LEE (Lens, desde D5.3) en algo que Kohana puede
/// PULSAR, y por eso vive aquí y no en `Vision`: leer un control e invocarlo no son la misma
/// capacidad, y mezclarlas haría que tener Lens implicara poder actuar.
///
/// La regla que gobierna la clase: **ante la duda, no se pulsa**.
///
/// - Sin coincidencia no se busca "algo parecido". Aproximar un nombre es pulsar un botón que nadie
///   pidió.
/// - **Con varias coincidencias tampoco se pulsa.** Es el caso importante: una ventana con tres
///   botones "Aceptar" no tiene un "el Aceptar", y elegir el primero del árbol es elegir al azar con
///   apariencia de criterio. Se dice cuántos hay y se pide precisión.
/// - Solo se invocan tipos de control que se pulsan. Un texto no se pulsa; pulsarlo "por si acaso"
///   es exactamente el tipo de acción cuyo efecto nadie puede predecir.
/// </summary>
public static class UiAutomationInvokePolicy
{
    /// <summary>
    /// Tipos que aceptan una invocación con efecto entendible. Lista de PERMITIDOS, como todo lo
    /// demás en esta fase: lo que no se reconoce no se pulsa.
    /// </summary>
    private static readonly string[] InvokableControlTypes =
    [
        "ControlType.Button",
        "ControlType.MenuItem",
        "ControlType.Hyperlink",
        "ControlType.CheckBox",
        "ControlType.RadioButton",
        "ControlType.TabItem",
        "ControlType.ListItem"
    ];

    public static InvokeVerdict Decide(UiAutomationSnapshot snapshot, string? controlName)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (!snapshot.IsSuccess)
        {
            return InvokeVerdict.Refused(
                InvokeRefusal.SinLectura,
                "No pude leer los controles de esa ventana, así que no pulso nada.");
        }

        if (string.IsNullOrWhiteSpace(controlName))
        {
            return InvokeVerdict.Refused(
                InvokeRefusal.NoEncontrado, "No me dijiste qué control pulsar.");
        }

        var needle = controlName.Trim();

        var matches = snapshot.Elements
            .Where(element => !string.IsNullOrWhiteSpace(element.Name))
            .Where(element => string.Equals(element.Name!.Trim(), needle, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (matches.Length == 0)
        {
            return InvokeVerdict.Refused(
                InvokeRefusal.NoEncontrado,
                $"No encuentro ningún control que se llame «{needle}» en esa ventana.");
        }

        if (matches.Length > 1)
        {
            return InvokeVerdict.Refused(
                InvokeRefusal.Ambiguo,
                $"Hay {matches.Length} controles llamados «{needle}» y no sé cuál quieres. " +
                "Dímelo de otra forma antes de que pulse el que no era.");
        }

        var target = matches[0];
        if (!InvokableControlTypes.Contains(target.ControlType, StringComparer.OrdinalIgnoreCase))
        {
            return InvokeVerdict.Refused(
                InvokeRefusal.NoInvocable,
                $"«{needle}» es un {FriendlyType(target.ControlType)}, no algo que se pulse.");
        }

        return InvokeVerdict.Allowed(target);
    }

    /// <summary>
    /// Diseño D19 — la ventana pasa por la MISMA exclusión que usa Lens
    /// (<see cref="VisionPrivacyPolicy"/>). Si Kohana no puede ni mirar un gestor de contraseñas,
    /// mucho menos pulsar dentro de él; darle a esta capacidad su propia lista sería la forma más
    /// fácil de que una de las dos se quedara corta.
    /// </summary>
    public static InvokeVerdict CheckWindow(string? windowTitle, string? processName) =>
        VisionPrivacyPolicy.IsSensitive(windowTitle, processName)
            ? InvokeVerdict.Refused(
                InvokeRefusal.VentanaSensible,
                "Esa ventana está marcada como sensible, así que no pulso nada dentro.")
            : new InvokeVerdict(InvokeRefusal.Permitido, "Ventana permitida.", null);

    private static string FriendlyType(string controlType) => controlType switch
    {
        "ControlType.Text" => "texto",
        "ControlType.Edit" => "campo de escritura",
        "ControlType.ComboBox" => "desplegable",
        _ => "elemento"
    };
}
