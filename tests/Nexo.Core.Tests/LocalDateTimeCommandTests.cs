using Nexo.Core.Commands;

namespace Nexo.Core.Tests;

public sealed class LocalDateTimeCommandTests
{
    private readonly NaturalCommandParser _parser = new();

    [Theory]
    [InlineData("qué día es hoy")]
    [InlineData("qué fecha es hoy")]
    [InlineData("dime la fecha")]
    public void Parse_RecognizesCurrentDate(string prompt)
    {
        var result = _parser.Parse(prompt);

        Assert.Equal(CommandRoute.Local, result.Route);
        Assert.Equal(LocalCommandType.ShowCurrentDate, result.Intent?.Type);
    }

    [Theory]
    [InlineData("qué hora es")]
    [InlineData("dime la hora")]
    [InlineData("hora actual")]
    public void Parse_RecognizesCurrentTime(string prompt)
    {
        var result = _parser.Parse(prompt);

        Assert.Equal(CommandRoute.Local, result.Route);
        Assert.Equal(LocalCommandType.ShowCurrentTime, result.Intent?.Type);
    }
}
