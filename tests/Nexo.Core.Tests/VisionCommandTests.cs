using Nexo.Core.Commands;

namespace Nexo.Core.Tests;

public sealed class VisionCommandTests
{
    private readonly NaturalCommandParser _parser = new();

    [Theory]
    [InlineData("mira esto")]
    [InlineData("Kohana, mira la pantalla")]
    [InlineData("Nexo, mira la pantalla")]
    [InlineData("analiza esto")]
    [InlineData("qué ves aquí")]
    [InlineData("ayúdame con este error")]
    public void Parse_RecognizesVisionCaptureCommands(string text)
    {
        var result = _parser.Parse(text);

        Assert.Equal(CommandRoute.Local, result.Route);
        Assert.Equal(LocalCommandType.CaptureForVision, result.Intent?.Type);
    }

    [Fact]
    public void Parse_VisualQuestionWithDetails_RemainsForAi()
    {
        var result = _parser.Parse("explica por qué este error aparece al compilar C#");

        Assert.Equal(CommandRoute.ArtificialIntelligence, result.Route);
    }
}
