using Nexo.Core.Commands;

namespace Nexo.Core.Tests;

public sealed class NaturalCommandParserTests
{
    private readonly NaturalCommandParser _parser = new();

    [Theory]
    [InlineData("muestra peek", LocalCommandType.ShowPeek)]
    [InlineData("muéstrame Peek", LocalCommandType.ShowPeek)]
    [InlineData("enseña Peek", LocalCommandType.ShowPeek)]
    [InlineData("muestra el Peek", LocalCommandType.ShowPeek)]
    [InlineData("Kohana, cómo está mi PC", LocalCommandType.ShowSystemStatus)]
    [InlineData("Oye Kohana, cómo está mi PC", LocalCommandType.ShowSystemStatus)]
    [InlineData("Nexo, cómo está mi PC", LocalCommandType.ShowSystemStatus)]
    [InlineData("cómo anda mi computadora", LocalCommandType.ShowSystemStatus)]
    [InlineData("abre PowerShell", LocalCommandType.OpenPowerShell)]
    [InlineData("ábreme el PowerShell", LocalCommandType.OpenPowerShell)]
    [InlineData("Exo abre audio", LocalCommandType.NavigateAudio)]
    public void Parse_RecognizesBasicLocalCommands(string text, LocalCommandType expectedType)
    {
        var result = _parser.Parse(text);

        Assert.Equal(CommandRoute.Local, result.Route);
        Assert.NotNull(result.Intent);
        Assert.Equal(expectedType, result.Intent.Type);
    }

    [Theory]
    [InlineData("Nexo baja el volumen de Spotify")]
    [InlineData("baja Spotify")]
    [InlineData("bájale a Spotify")]
    [InlineData("bajas Spotify")]
    public void Parse_LowerApplicationVolume_UsesHalfAsDefault(string text)
    {
        var result = _parser.Parse(text);

        Assert.Equal(CommandRoute.Local, result.Route);
        Assert.Equal(LocalCommandType.ScaleApplicationVolume, result.Intent?.Type);
        Assert.Equal("spotify", result.Intent?.Target);
        Assert.Equal(0.5, result.Intent?.Factor);
    }

    [Theory]
    [InlineData("baja Spotify al 50", 50)]
    [InlineData("bajas Spotify al 50", 50)]
    [InlineData("bájale a Spotify al 50", 50)]
    [InlineData("baja el volumen de Spotify al 50", 50)]
    [InlineData("deja Spotify en 50", 50)]
    [InlineData("subes Spotify al 70", 70)]
    [InlineData("sube Spotify al 100%", 100)]
    [InlineData("pon Spotify al cincuenta por ciento", 50)]
    public void Parse_SetApplicationVolume_AcceptsNaturalSpokenVariants(
        string text,
        double expectedPercent)
    {
        var result = _parser.Parse(text);

        Assert.Equal(CommandRoute.Local, result.Route);
        Assert.Equal(LocalCommandType.SetApplicationVolume, result.Intent?.Type);
        Assert.Equal("spotify", result.Intent?.Target);
        Assert.Equal(expectedPercent, result.Intent?.Percent);
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
    public void Parse_LowerSlightly_UsesMinusTenPoints()
    {
        var result = _parser.Parse("baja un poco Spotify");

        Assert.Equal(CommandRoute.Local, result.Route);
        Assert.Equal(LocalCommandType.ChangeApplicationVolume, result.Intent?.Type);
        Assert.Equal("spotify", result.Intent?.Target);
        Assert.Equal(-10, result.Intent?.DeltaPoints);
    }

    [Fact]
    public void Parse_RaiseWithoutPercentage_RemainsRelative()
    {
        var result = _parser.Parse("sube un poco Spotify");

        Assert.Equal(LocalCommandType.ChangeApplicationVolume, result.Intent?.Type);
        Assert.Equal("spotify", result.Intent?.Target);
        Assert.Equal(10, result.Intent?.DeltaPoints);
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


    [Theory]
    [InlineData("abre descargas", "downloads")]
    [InlineData("ábreme mis documentos", "documents")]
    [InlineData("abre mi escritorio", "desktop")]
    [InlineData("abre mis imágenes", "pictures")]
    public void Parse_OpenKnownFolder_RoutesLocally(string text, string expectedTarget)
    {
        var result = _parser.Parse(text);

        Assert.Equal(CommandRoute.Local, result.Route);
        Assert.Equal(LocalCommandType.OpenKnownFolder, result.Intent?.Type);
        Assert.Equal(expectedTarget, result.Intent?.Target);
    }

    [Theory]
    [InlineData("abre Visual Studio Code", "vscode")]
    [InlineData("abre la calculadora", "calculator")]
    [InlineData("abre el administrador de tareas", "taskmanager")]
    [InlineData("abre configuración de Windows", "windows-settings")]
    public void Parse_OpenKnownApplication_RoutesLocally(string text, string expectedTarget)
    {
        var result = _parser.Parse(text);

        Assert.Equal(CommandRoute.Local, result.Route);
        Assert.Equal(LocalCommandType.OpenKnownApplication, result.Intent?.Type);
        Assert.Equal(expectedTarget, result.Intent?.Target);
    }


    [Theory]
    [InlineData("descargas", LocalCommandType.OpenKnownFolder, "downloads")]
    [InlineData("explorador de archivos", LocalCommandType.OpenKnownApplication, "explorer")]
    [InlineData("visual studio code", LocalCommandType.OpenKnownApplication, "vscode")]
    [InlineData("calculadora", LocalCommandType.OpenKnownApplication, "calculator")]
    [InlineData("administrador de tareas", LocalCommandType.OpenKnownApplication, "taskmanager")]
    public void Parse_DirectKnownTarget_RoutesLocally(
        string text,
        LocalCommandType expectedType,
        string expectedTarget)
    {
        var result = _parser.Parse(text);

        Assert.Equal(CommandRoute.Local, result.Route);
        Assert.Equal(expectedType, result.Intent?.Type);
        Assert.Equal(expectedTarget, result.Intent?.Target);
    }

    [Fact]
    public void Parse_MiraEstaVentana_UsesVisionCapture()
    {
        var result = _parser.Parse("mira esta ventana");

        Assert.Equal(CommandRoute.Local, result.Route);
        Assert.Equal(LocalCommandType.CaptureForVision, result.Intent?.Type);
    }

    [Theory]
    [InlineData("Explícame por qué mi navegador usa tanta memoria")]
    [InlineData("por qué bajas Spotify cuando abro un juego")]
    public void Parse_OpenQuestion_IsRoutedToAi(string text)
    {
        var result = _parser.Parse(text);

        Assert.Equal(CommandRoute.ArtificialIntelligence, result.Route);
        Assert.Null(result.Intent);
    }

    [Fact]
    public void Normalize_RemovesAccentsAndPunctuation()
    {
        var normalized = NaturalCommandParser.Normalize("¡Kohana, cómo está mi PC?");

        Assert.Equal("kohana como esta mi pc", normalized);
    }
}
