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

    [Fact]
    public void VoiceCoordinator_HasNoWpfOrMainWindowOrPreferenceDependencies()
    {
        // Fase 1.3A: el coordinador debe ser mecánica de voz pura, sin WPF, sin
        // Dispatcher, sin conocer MainWindow, ShellPreferences ni ResourceGovernorDecision.
        var coordinatorPath = Path.Combine(
            RepositoryRoot, "src", "Nexo.Windows", "Voice", "VoiceCoordinator.cs");
        var content = File.ReadAllText(coordinatorPath);

        Assert.DoesNotContain("System.Windows", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Dispatcher", content, StringComparison.Ordinal);
        Assert.DoesNotContain("MainWindow", content, StringComparison.Ordinal);
        Assert.DoesNotContain("ShellPreferences", content, StringComparison.Ordinal);
        Assert.DoesNotContain("ResourceGovernorDecision", content, StringComparison.Ordinal);
        Assert.DoesNotContain("IServiceProvider", content, StringComparison.Ordinal);
    }

    [Fact]
    public void VoiceCoordinator_DoesNotDisposeTheThreeInjectedServices()
    {
        // Corrección obligatoria de propiedad de la subfase 1.3A: el coordinador no es
        // dueño del ciclo de vida de los tres servicios, así que su código fuente no debe
        // llamar Dispose() sobre ellos.
        var coordinatorPath = Path.Combine(
            RepositoryRoot, "src", "Nexo.Windows", "Voice", "VoiceCoordinator.cs");
        var content = File.ReadAllText(coordinatorPath);

        Assert.DoesNotContain("_voiceInputService.Dispose()", content, StringComparison.Ordinal);
        Assert.DoesNotContain("_voiceOutputService.Dispose()", content, StringComparison.Ordinal);
        Assert.DoesNotContain("_wakeWordService.Dispose()", content, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_StillOwnsTheThreeVoiceServicesAndTheirDisposalOrder()
    {
        // Subfase 1.3A: MainWindow todavía recibe los tres servicios directamente y
        // Window_Closed conserva exactamente el orden de liberación de la fase 1.2.
        var mainWindowPath = Path.Combine(RepositoryRoot, "src", "Nexo.App", "MainWindow.xaml.cs");
        var content = File.ReadAllText(mainWindowPath);

        Assert.Contains("IVoiceInputService? voiceInputService", content, StringComparison.Ordinal);
        Assert.Contains("IVoiceOutputService? voiceOutputService", content, StringComparison.Ordinal);
        Assert.Contains("IWakeWordService? wakeWordService", content, StringComparison.Ordinal);

        var disposeBlockStart = content.IndexOf(
            "_wakeWordService.WakeWordDetected -= WakeWordService_WakeWordDetected;",
            StringComparison.Ordinal);
        Assert.True(disposeBlockStart >= 0, "No se encontró el bloque de liberación de voz en Window_Closed.");

        var disposeBlock = content.Substring(disposeBlockStart, 400);
        var wakeWordUnsubIndex = disposeBlock.IndexOf(
            "_wakeWordService.WakeWordDetected -=", StringComparison.Ordinal);
        var recognitionUnsubIndex = disposeBlock.IndexOf(
            "_wakeWordService.RecognitionObserved -=", StringComparison.Ordinal);
        var wakeWordDisposeIndex = disposeBlock.IndexOf(
            "_wakeWordService.Dispose();", StringComparison.Ordinal);
        var aiChatDisposeIndex = disposeBlock.IndexOf(
            "disposableAiService.Dispose();", StringComparison.Ordinal);
        var voiceOutputDisposeIndex = disposeBlock.IndexOf(
            "_voiceOutputService.Dispose();", StringComparison.Ordinal);
        var voiceInputDisposeIndex = disposeBlock.IndexOf(
            "_voiceInputService.Dispose();", StringComparison.Ordinal);

        Assert.True(wakeWordUnsubIndex >= 0
            && recognitionUnsubIndex > wakeWordUnsubIndex
            && wakeWordDisposeIndex > recognitionUnsubIndex
            && aiChatDisposeIndex > wakeWordDisposeIndex
            && voiceOutputDisposeIndex > aiChatDisposeIndex
            && voiceInputDisposeIndex > voiceOutputDisposeIndex,
            "El orden de Dispose() en Window_Closed cambió respecto a la fase 1.2.");
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
