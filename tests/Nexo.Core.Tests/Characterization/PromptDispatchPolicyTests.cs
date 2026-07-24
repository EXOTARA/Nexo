using Nexo.Core.Automation;
using Nexo.Core.Commands;
using Nexo.Core.Focus;
using Nexo.Core.Tasks;

namespace Nexo.Core.Tests.Characterization;

/// <summary>
/// Fase 1.1.1 — regresión del **flujo completo** de despacho, con los cuatro parsers reales
/// conectados a <see cref="PromptDispatchPolicy"/>, no parsers aislados.
///
/// Corrige el defecto D1: el parser de rutinas capturaba cualquier frase que empezara por
/// "ejecuta/inicia/activa/corre" y eclipsaba a enfoque y tareas.
/// </summary>
public sealed class PromptDispatchPolicyTests
{
    private static readonly DateTimeOffset ReferenceNow =
        new(2026, 7, 23, 9, 0, 0, TimeSpan.FromHours(-6));

    private readonly SpanishRoutineCommandParser _routineParser = new();
    private readonly SpanishFocusCommandParser _focusParser = new();
    private readonly SpanishTaskCommandParser _taskParser = new();
    private readonly NaturalCommandParser _commandParser = new();

    /// <summary>
    /// Ejecuta la misma cadena que <c>MainWindow.ProcessPromptAsync</c>.
    /// </summary>
    private PromptDispatchDecision Dispatch(string prompt, params string[] existingRoutines)
    {
        // Los nombres se dan ya normalizados (minúsculas y sin acentos), que es la forma en
        // que `SpanishRoutineCommandParser` entrega `RoutineName`. En la aplicación real el
        // predicado lo resuelve `RoutineManager.FindBestMatch`.
        var known = new HashSet<string>(existingRoutines, StringComparer.OrdinalIgnoreCase);

        return PromptDispatchPolicy.Resolve(
            _routineParser.Parse(prompt),
            _focusParser.Parse(prompt),
            _taskParser.Parse(prompt, ReferenceNow),
            _commandParser.Parse(prompt),
            known.Contains);
    }

    // ---------- D1: el caso que estaba roto ----------

    [Theory]
    [InlineData("Inicia un temporizador de 20 minutos")]
    [InlineData("Inicia un descanso")]
    [InlineData("Inicia un pomodoro")]
    public void TimerOrders_StartFocus_EvenThoughTheyShareTheVerbWithRoutines(string prompt)
    {
        Assert.Equal(PromptDispatchTarget.Focus, Dispatch(prompt).Target);
    }

    [Fact]
    public void TimerOrders_StartFocus_EvenWhenRoutinesExist()
    {
        // La existencia de rutinas no debe reabrir el defecto.
        var decision = Dispatch(
            "Inicia un temporizador de 20 minutos",
            "estudio",
            "modo programacion");

        Assert.Equal(PromptDispatchTarget.Focus, decision.Target);
    }

    // ---------- Las rutinas siguen funcionando ----------

    [Theory]
    [InlineData("ejecuta la rutina estudio")]
    [InlineData("inicia la rutina estudio")]
    [InlineData("activa la rutina estudio")]
    [InlineData("corre la rutina estudio")]
    [InlineData("inicia mi rutina estudio")]
    [InlineData("inicia la rutina llamada estudio")]
    public void ExplicitRoutineOrders_AlwaysRunTheRoutine(string prompt)
    {
        Assert.Equal(PromptDispatchTarget.Routine, Dispatch(prompt, "estudio").Target);
    }

    [Fact]
    public void AnExplicitRoutineOrder_WinsEvenIfNoRoutineMatches()
    {
        // Si el usuario nombró el dominio, merece el mensaje "no encontré esa rutina",
        // no un desvío silencioso a la IA.
        var decision = Dispatch("ejecuta la rutina inexistente");

        Assert.Equal(PromptDispatchTarget.Routine, decision.Target);
    }

    [Fact]
    public void AnExplicitRoutineOrder_BeatsFocusWhenTheNameCollides()
    {
        // Vía de escape del usuario: su rutina se llama igual que una orden de enfoque.
        var decision = Dispatch(
            "inicia la rutina un temporizador de 20 minutos",
            "un temporizador de 20 minutos");

        Assert.Equal(PromptDispatchTarget.Routine, decision.Target);
    }

    [Fact]
    public void ModeOrders_AreExplicitRoutineOrders()
    {
        Assert.Equal(
            PromptDispatchTarget.Routine,
            Dispatch("Oye Kohana modo programación", "modo programacion").Target);
    }

    [Theory]
    [InlineData("abre rutinas")]
    [InlineData("muestra mis rutinas")]
    [InlineData("lista mis rutinas")]
    public void RoutineNavigationOrders_AreExplicit(string prompt)
    {
        Assert.Equal(PromptDispatchTarget.Routine, Dispatch(prompt).Target);
    }

    // ---------- Hipótesis de rutina ----------

    [Fact]
    public void AnInferredRoutineOrder_RunsWhenTheRoutineReallyExists()
    {
        var decision = Dispatch("inicia estudio", "estudio");

        Assert.Equal(PromptDispatchTarget.Routine, decision.Target);
    }

    [Fact]
    public void AnInferredRoutineOrder_DoesNotRunWhenNoRoutineMatches()
    {
        // Requisito: una frase ambigua no debe ejecutar una rutina equivocada.
        var decision = Dispatch("inicia estudio");

        Assert.NotEqual(PromptDispatchTarget.Routine, decision.Target);
    }

    [Fact]
    public void AnInferredRoutineOrder_NeverRunsADifferentRoutine()
    {
        // Existe "estudio" pero el usuario pidió "concentracion": no debe caer en "estudio".
        var decision = Dispatch("inicia concentracion", "estudio");

        Assert.NotEqual(PromptDispatchTarget.Routine, decision.Target);
    }

    [Fact]
    public void TheParserMarksSharedVerbsAsInferred()
    {
        Assert.True(_routineParser.Parse("inicia estudio").IsInferred);
        Assert.False(_routineParser.Parse("inicia la rutina estudio").IsInferred);
        Assert.False(_routineParser.Parse("modo programación").IsInferred);
        Assert.False(_routineParser.Parse("abre rutinas").IsInferred);
    }

    [Fact]
    public void AnInferredOrderKeepsTheNameWithoutTheRoutineKeyword()
    {
        Assert.Equal("estudio", _routineParser.Parse("inicia estudio").RoutineName);
        Assert.Equal("estudio", _routineParser.Parse("inicia la rutina estudio").RoutineName);
    }

    // ---------- El resto de la precedencia sigue intacta ----------

    [Theory]
    [InlineData("Abre enfoque")]
    [InlineData("Pausa el temporizador")]
    [InlineData("Cancela el temporizador")]
    [InlineData("¿Cuánto tiempo me queda?")]
    public void FocusOrders_StillGoToFocus(string prompt)
    {
        Assert.Equal(PromptDispatchTarget.Focus, Dispatch(prompt).Target);
    }

    [Theory]
    [InlineData("Abre tareas")]
    [InlineData("¿Qué tengo pendiente hoy?")]
    [InlineData("Recuérdame llamar a mamá")]
    public void TaskOrders_StillGoToTasks(string prompt)
    {
        Assert.Equal(PromptDispatchTarget.Task, Dispatch(prompt).Target);
    }

    [Theory]
    [InlineData("abre PowerShell")]
    [InlineData("abre la calculadora")]
    [InlineData("abre descargas")]
    [InlineData("muestra peek")]
    [InlineData("silencia Discord")]
    [InlineData("mira esta ventana")]
    public void KnownLocalCommands_StillNeverReachTheAi(string prompt)
    {
        Assert.Equal(PromptDispatchTarget.LocalCommand, Dispatch(prompt).Target);
    }

    [Theory]
    [InlineData("Explícame por qué mi navegador usa tanta memoria")]
    [InlineData("Explícame la memoria RAM")]
    public void OpenQuestions_StillGoToTheAi(string prompt)
    {
        Assert.True(Dispatch(prompt).GoesToAi);
    }

    [Fact]
    public void LocalCommandsAreNeverDispatchedWithoutAnIntent()
    {
        // MainWindow desreferencia `interpretation.Intent` en la rama LocalCommand.
        // Esta prueba garantiza que la política nunca la elige sin intención.
        var decision = PromptDispatchPolicy.Resolve(
            RoutineCommand.None("x"),
            FocusCommand.None("x"),
            TaskCommand.None("x"),
            new CommandInterpretation(CommandRoute.Local, "x", "x", Intent: null),
            _ => false);

        Assert.True(decision.GoesToAi);
    }

    [Fact]
    public void EveryDecisionCarriesAReason()
    {
        foreach (var prompt in new[]
                 {
                     "inicia la rutina estudio",
                     "Inicia un temporizador de 20 minutos",
                     "Abre tareas",
                     "abre PowerShell",
                     "Explícame la memoria RAM"
                 })
        {
            Assert.False(string.IsNullOrWhiteSpace(Dispatch(prompt).Reason));
        }
    }

    [Fact]
    public void ThePolicyRejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => PromptDispatchPolicy.Resolve(
            RoutineCommand.None("x"),
            FocusCommand.None("x"),
            TaskCommand.None("x"),
            new CommandInterpretation(CommandRoute.Local, "x", "x"),
            routineExists: null!));
    }
}
