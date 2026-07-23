namespace Nexo.Windows.Tests.Composition;

/// <summary>
/// Invariantes estructurales de la fase 1.2 que no pueden expresarse como una llamada a la API:
/// dependen del contenido de archivos del repositorio. <c>Nexo.App</c> no se referencia desde
/// pruebas porque arrastra <c>UseWPF</c> (ver notas de la fase 1.1), así que estas pruebas leen
/// el código fuente y el project file directamente.
/// </summary>
public sealed class CompositionInvariantTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void NexoCoreProject_HasNoPackageReferences()
    {
        var csprojPath = Path.Combine(RepositoryRoot, "src", "Nexo.Core", "Nexo.Core.csproj");
        var content = File.ReadAllText(csprojPath);

        Assert.DoesNotContain("PackageReference", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MainWindow_DoesNotReferenceIServiceProvider()
    {
        var mainWindowPath = Path.Combine(RepositoryRoot, "src", "Nexo.App", "MainWindow.xaml.cs");
        var content = File.ReadAllText(mainWindowPath);

        Assert.DoesNotContain("IServiceProvider", content, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_NoLongerConstructsTheSixServicesDirectly()
    {
        var mainWindowPath = Path.Combine(RepositoryRoot, "src", "Nexo.App", "MainWindow.xaml.cs");
        var content = File.ReadAllText(mainWindowPath);

        Assert.DoesNotContain("= new AiChatRouterService()", content, StringComparison.Ordinal);
        Assert.DoesNotContain("= new WindowsAudioMixerService()", content, StringComparison.Ordinal);
        Assert.DoesNotContain("= new WhisperVoiceInputService()", content, StringComparison.Ordinal);
        Assert.DoesNotContain("= new WindowsTextToSpeechService()", content, StringComparison.Ordinal);
        Assert.DoesNotContain("= new VoskWakeWordService()", content, StringComparison.Ordinal);
        Assert.DoesNotContain("= new WindowsScreenCaptureService()", content, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Nexo.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("No se encontró Nexo.slnx desde el directorio de pruebas.");
    }
}
