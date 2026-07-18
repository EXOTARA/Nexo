using Nexo.Core.Commands;

namespace Nexo.Core.Tests;

public sealed class NaturalCommandParserTests
{
    private readonly NaturalCommandParser _parser = new();

    [Theory]
    [InlineData("muestra peek", LocalCommandType.ShowPeek)]
    [InlineData("Nexo, cómo está mi PC", LocalCommandType.ShowSystemStatus)]
    [InlineData("abre PowerShell", LocalCommandType.OpenPowerShell)]
    [InlineData("Exo abre audio", LocalCommandType.NavigateAudio)]
    public void Parse_RecognizesBasicLocalCommands(string text, LocalCommandType expectedType)
    {
        var result = _parser.Parse(text);

        Assert.Equal(CommandRoute.Local, result.Route);
        Assert.NotNull(result.Intent);
        Assert.Equal(expectedType, result.Intent.Type);
    }

    [Fact]
    public void Parse_LowerApplicationVolume_UsesHalfAsDefault()
    {
        var result = _parser.Parse("Nexo baja el volumen de Spotify");

        Assert.Equal(CommandRoute.Local, result.Route);
        Assert.Equal(LocalCommandType.ScaleApplicationVolume, result.Intent?.Type);
        Assert.Equal("spotify", result.Intent?.Target);
        Assert.Equal(0.5, result.Intent?.Factor);
    }

    [Fact]
    public void Parse_SetApplicationVolume_ClampsPercent()
    {
        var result = _parser.Parse("pon Spotify al 140 por ciento");

        Assert.Equal(LocalCommandType.SetApplicationVolume, result.Intent?.Type);
        Assert.Equal("spotify", result.Intent?.Target);
        Assert.Equal(100, result.Intent?.Percent);
    }

    [Fact]
    public void Parse_MuteAndUnmute_AreDifferentCommands()
    {
        var mute = _parser.Parse("silencia Discord");
        var unmute = _parser.Parse("quita el silencio de Discord");

        Assert.Equal(LocalCommandType.MuteApplication, mute.Intent?.Type);
        Assert.Equal(LocalCommandType.UnmuteApplication, unmute.Intent?.Type);
        Assert.Equal("discord", mute.Intent?.Target);
        Assert.Equal("discord", unmute.Intent?.Target);
    }

    [Fact]
    public void Parse_OpenQuestion_IsRoutedToAi()
    {
        var result = _parser.Parse("Explícame por qué mi navegador usa tanta memoria");

        Assert.Equal(CommandRoute.ArtificialIntelligence, result.Route);
        Assert.Null(result.Intent);
    }

    [Fact]
    public void Normalize_RemovesAccentsAndPunctuation()
    {
        var normalized = NaturalCommandParser.Normalize("¡Nexo, cómo está mi PC?");

        Assert.Equal("nexo como esta mi pc", normalized);
    }
}
