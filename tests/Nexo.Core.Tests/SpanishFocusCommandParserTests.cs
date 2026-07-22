using Nexo.Core.Focus;

namespace Nexo.Core.Tests;

public sealed class SpanishFocusCommandParserTests
{
    private readonly SpanishFocusCommandParser _parser = new();

    [Theory]
    [InlineData("Abre enfoque")]
    [InlineData("Kohana abre temporizador")]
    [InlineData("Nexo abre temporizador")]
    public void Parse_OpenFocus_IsRecognized(string text)
    {
        Assert.Equal(FocusCommandType.OpenFocus, _parser.Parse(text).Type);
    }

    [Fact]
    public void Parse_CustomTimer_ExtractsMinutes()
    {
        var result = _parser.Parse("Inicia un temporizador de 20 minutos");

        Assert.Equal(FocusCommandType.Start, result.Type);
        Assert.Equal(TimeSpan.FromMinutes(20), result.Duration);
        Assert.Equal(FocusSessionKind.Custom, result.Kind);
    }

    [Fact]
    public void Parse_StudySession_ExtractsKindAndDuration()
    {
        var result = _parser.Parse(
            "Comienza una sesión de estudio de cuarenta minutos");

        Assert.Equal(FocusCommandType.Start, result.Type);
        Assert.Equal(TimeSpan.FromMinutes(40), result.Duration);
        Assert.Equal(FocusSessionKind.Study, result.Kind);
    }

    [Fact]
    public void Parse_BreakWithoutDuration_UsesFiveMinutes()
    {
        var result = _parser.Parse("Inicia un descanso");

        Assert.Equal(FocusCommandType.Start, result.Type);
        Assert.Equal(TimeSpan.FromMinutes(5), result.Duration);
        Assert.Equal(FocusSessionKind.Break, result.Kind);
    }

    [Fact]
    public void Parse_PomodoroWithoutDuration_UsesTwentyFiveMinutes()
    {
        var result = _parser.Parse("Inicia un pomodoro");

        Assert.Equal(FocusCommandType.Start, result.Type);
        Assert.Equal(TimeSpan.FromMinutes(25), result.Duration);
        Assert.Equal(FocusSessionKind.Focus, result.Kind);
    }

    [Theory]
    [InlineData("Pausa el temporizador", FocusCommandType.Pause)]
    [InlineData("Continúa el temporizador", FocusCommandType.Resume)]
    [InlineData("Cancela el temporizador", FocusCommandType.Cancel)]
    [InlineData("¿Cuánto tiempo me queda?", FocusCommandType.Status)]
    public void Parse_ControlCommands_AreRecognized(
        string text,
        FocusCommandType expected)
    {
        Assert.Equal(expected, _parser.Parse(text).Type);
    }

    [Fact]
    public void Parse_Hours_ConvertsToTimeSpan()
    {
        var result = _parser.Parse("Pon un temporizador de una hora");

        Assert.Equal(TimeSpan.FromHours(1), result.Duration);
    }

    [Fact]
    public void Parse_UnrelatedQuestion_ReturnsNone()
    {
        Assert.Equal(
            FocusCommandType.None,
            _parser.Parse("Explícame la memoria RAM").Type);
    }
}
