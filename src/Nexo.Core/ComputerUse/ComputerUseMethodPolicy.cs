namespace Nexo.Core.ComputerUse;

/// <summary>Diseño D17 — un método descartado y el motivo. Los descartes se enseñan, no se ocultan.</summary>
public sealed record ComputerUseMethodRejection(ComputerUseMethod Method, string Reason);

public sealed record ComputerUseMethodChoice(
    ComputerUseMethod? Method,
    string Reason,
    IReadOnlyList<ComputerUseMethodRejection> Rejected)
{
    public bool HasMethod => Method is not null;
}

/// <summary>
/// Diseño D17 (Fase 7) — elige el método más seguro que esté DISPONIBLE, recorriendo la lista de
/// arriba abajo. Nunca al revés, nunca saltándose uno porque otro sea más cómodo.
///
/// Dos reglas que no son negociables:
///
/// 1. **No se baja de escalón mientras haya uno más arriba disponible.** Si existe la API oficial, no
///    se usa UI Automation aunque UI Automation "también funcione". Es toda la diferencia entre una
///    automatización que se rompe con una actualización y una que no.
/// 2. **<see cref="ComputerUseMethod.RatonTeclado"/> exige que la persona lo haya habilitado a
///    propósito.** Es el único método que no puede comprobar qué hizo, y el roadmap lo marca como
///    último recurso. Que esté disponible en el equipo no basta: hace falta que alguien haya dicho
///    que sí.
/// </summary>
public static class ComputerUseMethodPolicy
{
    public static ComputerUseMethodChoice Choose(
        IEnumerable<ComputerUseMethod> availableMethods,
        bool simulatedInputAllowed = false)
    {
        var available = (availableMethods ?? [])
            .Where(Enum.IsDefined)
            .Distinct()
            .OrderBy(method => (int)method)
            .ToArray();

        var rejected = new List<ComputerUseMethodRejection>();

        foreach (var method in available)
        {
            if (method == ComputerUseMethod.RatonTeclado && !simulatedInputAllowed)
            {
                rejected.Add(new ComputerUseMethodRejection(
                    method,
                    "Ratón y teclado simulados están desactivados. Son el último recurso y hay que " +
                    "habilitarlos a propósito."));
                continue;
            }

            return new ComputerUseMethodChoice(
                method,
                $"Uso {ComputerUseMethodText.Describe(method)}. {ComputerUseMethodText.WhyItIsSafe(method)}",
                rejected);
        }

        return new ComputerUseMethodChoice(
            null,
            available.Length == 0
                ? "No hay ninguna forma segura de hacer esto en este equipo, así que no lo intento."
                : "Los métodos disponibles están descartados, así que no lo intento.",
            rejected);
    }

    /// <summary>
    /// Diseño D17 — comprobación explícita de la regla 1, pensada para poder probarla y para que
    /// cualquier camino futuro que elija método pueda verificarse contra ella.
    /// </summary>
    public static bool IsSafestAvailable(
        ComputerUseMethod chosen,
        IEnumerable<ComputerUseMethod> availableMethods,
        bool simulatedInputAllowed = false) =>
        !(availableMethods ?? [])
            .Where(Enum.IsDefined)
            .Where(method => method != ComputerUseMethod.RatonTeclado || simulatedInputAllowed)
            .Any(method => (int)method < (int)chosen);
}
