using Nexo.Core.Automation;
using Nexo.Core.Focus;
using Nexo.Core.Tasks;

namespace Nexo.Core.Commands;

/// <summary>
/// Subsistema al que se entrega un prompt del usuario.
/// </summary>
public enum PromptDispatchTarget
{
    Routine,
    Focus,
    Task,
    LocalCommand,
    ArtificialIntelligence
}

/// <summary>
/// Decisión de despacho, con el motivo. El motivo no es decorativo: es lo que permite
/// explicar al usuario por qué su frase fue a un sitio y no a otro, y lo que hace la
/// precedencia auditable en pruebas.
/// </summary>
public sealed record PromptDispatchDecision(
    PromptDispatchTarget Target,
    string Reason)
{
    public bool GoesToAi => Target == PromptDispatchTarget.ArtificialIntelligence;
}

/// <summary>
/// Regla de precedencia entre los cuatro parsers del shell.
///
/// <para>
/// <b>Por qué existe (defecto D1 de la fase 1.1).</b> Antes, <c>MainWindow</c> consultaba los
/// parsers en cascada y se quedaba con el primero que reclamara la frase. Como el parser de
/// rutinas corría primero y capturaba cualquier texto que empezara por
/// "ejecuta/inicia/activa/corre", órdenes como <i>"Inicia un temporizador de 20 minutos"</i>
/// nunca llegaban al parser de enfoque; y como el shell no reintentaba, terminaban en
/// "no encontré una rutina" sin arrancar nada.
/// </para>
///
/// <para>
/// <b>Cómo se resuelve.</b> No con una lista de excepciones, sino distinguiendo *cómo* reclamó
/// el parser de rutinas la frase (<see cref="RoutineMatchConfidence"/>) y exigiendo que una
/// hipótesis se confirme contra las rutinas que existen de verdad. La regla es una función
/// pura y se prueba sin WPF.
/// </para>
///
/// <para><b>Orden normativo:</b></para>
/// <list type="number">
///   <item>Rutina <b>explícita</b> (el texto dice "rutina", "modo X", "abre mis rutinas").</item>
///   <item>Enfoque.</item>
///   <item>Tareas.</item>
///   <item>Rutina <b>inferida</b>, y solo si existe una rutina con ese nombre.</item>
///   <item>Orden local conocida.</item>
///   <item>IA, como último recurso.</item>
/// </list>
/// </summary>
public static class PromptDispatchPolicy
{
    /// <param name="routineExists">
    /// Predicado que responde si hay una rutina habilitada que case con el nombre. Se inyecta
    /// para que la política siga siendo pura y comprobable sin almacén de rutinas.
    /// </param>
    public static PromptDispatchDecision Resolve(
        RoutineCommand routineCommand,
        FocusCommand focusCommand,
        TaskCommand taskCommand,
        CommandInterpretation interpretation,
        Func<string, bool> routineExists)
    {
        ArgumentNullException.ThrowIfNull(routineCommand);
        ArgumentNullException.ThrowIfNull(focusCommand);
        ArgumentNullException.ThrowIfNull(taskCommand);
        ArgumentNullException.ThrowIfNull(interpretation);
        ArgumentNullException.ThrowIfNull(routineExists);

        var routineClaims = routineCommand.Type != RoutineCommandType.None;

        // 1. Una petición explícita de rutina gana siempre. Es la vía de escape del usuario
        //    cuando el nombre de su rutina choca con otro dominio: "inicia la rutina X".
        if (routineClaims && !routineCommand.IsInferred)
        {
            return new PromptDispatchDecision(
                PromptDispatchTarget.Routine,
                "El texto nombra explícitamente una rutina.");
        }

        // 2 y 3. Los dominios específicos ganan a una hipótesis de rutina.
        if (focusCommand.Type != FocusCommandType.None)
        {
            return new PromptDispatchDecision(
                PromptDispatchTarget.Focus,
                "La orden corresponde a enfoque o temporizador.");
        }

        if (taskCommand.Type != TaskCommandType.None)
        {
            return new PromptDispatchDecision(
                PromptDispatchTarget.Task,
                "La orden corresponde a tareas.");
        }

        // 4. La hipótesis de rutina solo se confirma si la rutina existe. Así una frase
        //    ambigua nunca ejecuta una rutina equivocada ni muere en "no encontré una rutina".
        if (routineClaims && routineCommand.IsInferred)
        {
            if (routineExists(routineCommand.RoutineName))
            {
                return new PromptDispatchDecision(
                    PromptDispatchTarget.Routine,
                    "Existe una rutina que coincide con el nombre indicado.");
            }

            // No existe: se descarta la hipótesis y se sigue evaluando.
        }

        // 5 y 6.
        if (interpretation.Route == CommandRoute.ArtificialIntelligence ||
            interpretation.Intent is null)
        {
            return new PromptDispatchDecision(
                PromptDispatchTarget.ArtificialIntelligence,
                "Ningún subsistema local reclamó la frase.");
        }

        return new PromptDispatchDecision(
            PromptDispatchTarget.LocalCommand,
            "La orden es un comando local conocido.");
    }
}
