using Nexo.Core.Automation;

namespace Nexo.Core.Tests;

public sealed class SpanishRoutineCommandParserTests
{
    private readonly SpanishRoutineCommandParser _parser = new();

    [Theory]
    [InlineData("abre rutinas")]
    [InlineData("Nexo muestra mis rutinas")]
    public void Parse_OpenRoutines_IsLocal(string input)
    {
        var result = _parser.Parse(input);
        Assert.Equal(RoutineCommandType.OpenRoutines, result.Type);
    }

    [Fact]
    public void Parse_ModeProgramming_ExtractsRoutine()
    {
        var result = _parser.Parse("Nexo modo programación");

        Assert.Equal(RoutineCommandType.RunRoutine, result.Type);
        Assert.Equal("modo programacion", result.RoutineName);
    }

    [Fact]
    public void Parse_ExecuteNamedRoutine_ExtractsName()
    {
        var result = _parser.Parse("ejecuta la rutina estudio");

        Assert.Equal(RoutineCommandType.RunRoutine, result.Type);
        Assert.Equal("estudio", result.RoutineName);
    }

    [Fact]
    public void Parse_ListRoutines_IsDetected()
    {
        var result = _parser.Parse("lista mis rutinas");
        Assert.Equal(RoutineCommandType.ListRoutines, result.Type);
    }

    [Fact]
    public void Parse_UnrelatedPrompt_ReturnsNone()
    {
        var result = _parser.Parse("explícame qué es una rutina");
        Assert.Equal(RoutineCommandType.None, result.Type);
    }
}
