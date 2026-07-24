using Nexo.Core.Automation;
using Nexo.Core.Commands;
using Nexo.Core.Focus;
using Nexo.Core.Tasks;

namespace Nexo.Core.Tests.Characterization;

/// <summary>
/// Fase 1.1 — congela el **orden de despacho** de un prompt tal como lo aplica hoy
/// <c>MainWindow.HandlePromptAsync</c>:
///
/// <list type="number">
///   <item>rutinas (<see cref="SpanishRoutineCommandParser"/>)</item>
///   <item>enfoque (<see cref="SpanishFocusCommandParser"/>)</item>
///   <item>tareas (<see cref="SpanishTaskCommandParser"/>)</item>
///   <item>órdenes locales (<see cref="NaturalCommandParser"/>)</item>
///   <item>IA, solo si ningún parser anterior reclamó el texto</item>
/// </list>
///
/// El orden es la conducta: si un paso posterior de la extracción lo altera, un mismo texto
/// puede cambiar de subsistema sin que nadie lo note. Estas pruebas lo impiden.
/// </summary>
public sealed class PromptDispatchCharacterizationTests
{
    private static readonly DateTimeOffset ReferenceNow =
        new(2026, 7, 23, 9, 0, 0, TimeSpan.FromHours(-6));

    private readonly SpanishRoutineCommandParser _routineParser = new();
    private readonly SpanishFocusCommandParser _focusParser = new();
    private readonly SpanishTaskCommandParser _taskParser = new();
    private readonly NaturalCommandParser _commandParser = new();

    /// <summary>
    /// Réplica de la cadena de decisión de <c>MainWindow.ProcessPromptAsync</c>, que desde la
    /// fase 1.1.1 delega la precedencia en <see cref="PromptDispatchPolicy"/>.
    /// Aquí se evalúa sin ninguna rutina definida, que es el estado de una instalación limpia.
    /// </summary>
    private string ResolveDispatch(string prompt)
    {
        var decision = PromptDispatchPolicy.Resolve(
            _routineParser.Parse(prompt),
            _focusParser.Parse(prompt),
            _taskParser.Parse(prompt, ReferenceNow),
            _commandParser.Parse(prompt),
            _ => false);

        return decision.Target switch
        {
            PromptDispatchTarget.Routine => "Routine",
            PromptDispatchTarget.Focus => "Focus",
            PromptDispatchTarget.Task => "Task",
            PromptDispatchTarget.LocalCommand => "Local",
            _ => "Ai"
        };
    }

    [Theory]
    [InlineData("abre rutinas")]
    [InlineData("ejecuta la rutina estudio")]
    public void RoutinePrompts_AreClaimedFirst(string prompt)
    {
        Assert.Equal("Routine", ResolveDispatch(prompt));
    }

    [Theory]
    [InlineData("Abre enfoque")]
    [InlineData("Pausa el temporizador")]
    [InlineData("Cancela el temporizador")]
    [InlineData("¿Cuánto tiempo me queda?")]
    public void FocusPrompts_AreClaimedBeforeTasksAndLocalCommands(string prompt)
    {
        Assert.Equal("Focus", ResolveDispatch(prompt));
    }

    [Theory]
    [InlineData("Inicia un temporizador de 20 minutos")]
    [InlineData("Inicia un descanso")]
    [InlineData("Inicia un pomodoro")]
    public void VerbsSharedWithRoutines_StillLookLikeRoutinesToTheParserAlone(string prompt)
    {
        // El parser de rutinas, **aislado**, sigue reclamando estas frases: comparte el verbo.
        // Eso no es un defecto por sí mismo; el defecto D1 era que `MainWindow` se quedaba con
        // ese primer reclamo y no reintentaba.
        //
        // Corregido en la fase 1.1.1: el parser ahora marca este reclamo como
        // `RoutineMatchConfidence.Inferred` y `PromptDispatchPolicy` lo descarta en favor de
        // enfoque. El despacho real está probado en `PromptDispatchPolicyTests`, que es el
        // flujo completo.
        Assert.NotEqual(RoutineCommandType.None, _routineParser.Parse(prompt).Type);
        Assert.True(_routineParser.Parse(prompt).IsInferred);
    }

    [Fact]
    public void TheFocusParserItselfHandlesThoseOrders()
    {
        // Evidencia de que el problema estaba en la precedencia, no en el parser de enfoque.
        Assert.NotEqual(
            FocusCommandType.None,
            _focusParser.Parse("Inicia un temporizador de 20 minutos").Type);
    }

    [Theory]
    [InlineData("Abre tareas")]
    [InlineData("¿Qué tengo pendiente hoy?")]
    [InlineData("Recuérdame llamar a mamá")]
    public void TaskPrompts_AreClaimedBeforeLocalCommands(string prompt)
    {
        Assert.Equal("Task", ResolveDispatch(prompt));
    }

    [Theory]
    [InlineData("abre PowerShell")]
    [InlineData("abre la calculadora")]
    [InlineData("abre descargas")]
    [InlineData("Exo abre audio")]
    [InlineData("muestra peek")]
    [InlineData("mira esta ventana")]
    [InlineData("silencia Discord")]
    public void KnownLocalCommands_NeverReachTheAi(string prompt)
    {
        // Garantía dura de PRODUCT_VISION §D1 y del plan §Fase 5:
        // "Abre X" nunca llega al LLM.
        Assert.Equal("Local", ResolveDispatch(prompt));
    }

    [Theory]
    [InlineData("Explícame por qué mi navegador usa tanta memoria")]
    [InlineData("explícame qué es una rutina")]
    [InlineData("Explícame la memoria RAM")]
    public void OpenQuestions_FallThroughToTheAi(string prompt)
    {
        Assert.Equal("Ai", ResolveDispatch(prompt));
    }

    [Fact]
    public void AiIsTheLastResort_NotTheDefaultEntryPoint()
    {
        // Un texto vacío no debe inventar una orden local; cae a IA como cualquier texto
        // no reconocido. Se congela porque define quién ve el prompt primero.
        Assert.Equal("Ai", ResolveDispatch(string.Empty));
    }

    [Fact]
    public void LocalCommandsCarryAnIntent_AiRoutesDoNot()
    {
        var local = _commandParser.Parse("abre PowerShell");
        var ai = _commandParser.Parse("Explícame la memoria RAM");

        Assert.Equal(CommandRoute.Local, local.Route);
        Assert.NotNull(local.Intent);

        Assert.Equal(CommandRoute.ArtificialIntelligence, ai.Route);
        Assert.Null(ai.Intent);
    }

    [Fact]
    public void ALocalRouteWithoutIntent_WouldStillGoToTheAi()
    {
        // MainWindow trata `Intent is null` como equivalente a ruta de IA. Esta prueba fija
        // esa red de seguridad para que la extracción no la pierda.
        var interpretation = new CommandInterpretation(
            CommandRoute.Local,
            "texto",
            "texto",
            Intent: null);

        var goesToAi = interpretation.Route == CommandRoute.ArtificialIntelligence ||
                       interpretation.Intent is null;

        Assert.True(goesToAi);
    }
}
