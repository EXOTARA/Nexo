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

    // ---------- Fase 1.3B1: VoiceCoordinator inyectado en MainWindow ----------

    [Fact]
    public void App_PassesTheCompositionRootsVoiceCoordinatorToMainWindow()
    {
        var content = ReadAppSource();

        Assert.Contains("_compositionRoot.VoiceCoordinator", content, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_ReceivesVoiceCoordinatorAsATypedConstructorDependency()
    {
        var content = ReadMainWindowSource();

        Assert.Contains("VoiceCoordinator? voiceCoordinator = null", content, StringComparison.Ordinal);
        Assert.Contains("private readonly VoiceCoordinator _voiceCoordinator;", content, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_FallbackWrapsExistingServices_NeverBuildsAFourthEngineSet()
    {
        var content = ReadMainWindowSource();

        // El valor por defecto (solo para construcción directa fuera de App.OnStartup)
        // envuelve los mismos tres campos ya resueltos arriba: nunca construye un motor
        // adicional ni un segundo VoiceCoordinator "real" — App.OnStartup siempre provee
        // el suyo, verificado en App_PassesTheCompositionRootsVoiceCoordinatorToMainWindow.
        Assert.Contains(
            "?? new VoiceCoordinator(_voiceInputService, _voiceOutputService, _wakeWordService)",
            content, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigureVoiceInputDevices_RoutesEnumerationAndSelectionThroughTheCoordinator()
    {
        var body = ExtractMethodBody(
            ReadMainWindowSource(),
            "private void ConfigureVoiceInputDevices()",
            "private async Task ChangeVoiceInputDeviceAsync");

        Assert.Contains("_voiceCoordinator.GetInputDevices()", body, StringComparison.Ordinal);
        Assert.Contains("_voiceCoordinator.InputDeviceNumber = selectedDeviceNumber;", body, StringComparison.Ordinal);
        Assert.DoesNotContain("_voiceInputService.GetInputDevices()", body, StringComparison.Ordinal);
        Assert.DoesNotContain("_voiceInputService.InputDeviceNumber", body, StringComparison.Ordinal);
        Assert.DoesNotContain("_wakeWordService.InputDeviceNumber", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ChangeVoiceInputDeviceAsync_UsesCoordinatorForDeviceSelection_ButKeepsDirectCancelCall()
    {
        var body = ExtractMethodBody(
            ReadMainWindowSource(),
            "private async Task ChangeVoiceInputDeviceAsync",
            "private async Task<bool> TryHandlePendingVoiceDecisionAsync(");

        Assert.Contains("_voiceCoordinator.InputDeviceNumber = deviceNumber;", body, StringComparison.Ordinal);
        // `.GetInputDevices()` se llama encadenado en varias líneas (`_voiceCoordinator`
        // seguido de `.GetInputDevices()` en la línea siguiente); se comprueban por
        // separado para no depender del formato exacto del encadenamiento.
        Assert.Contains("_voiceCoordinator", body, StringComparison.Ordinal);
        Assert.Contains(".GetInputDevices()", body, StringComparison.Ordinal);
        Assert.Contains("_voiceCoordinator.IsVoiceInputReady", body, StringComparison.Ordinal);

        // CancelAsync() se conserva sin migrar a propósito: VoiceCoordinator solo expone
        // CancelPushToTalkAsync, que añade su propio candado de voz interno no presente
        // hoy en esta ruta — no es una equivalencia exacta (ver informe de la subfase 1.3B1).
        Assert.Contains("await _voiceInputService.CancelAsync();", body, StringComparison.Ordinal);
    }

    [Fact]
    public void PrepareVoiceAsync_RoutesReadinessAndPreparationThroughTheCoordinator()
    {
        var body = ExtractMethodBody(
            ReadMainWindowSource(),
            "private async Task PrepareVoiceAsync()",
            "private async Task InitializeVoiceFeaturesAsync()");

        Assert.Contains("_voiceCoordinator.IsVoiceInputReady", body, StringComparison.Ordinal);
        Assert.Contains("_voiceCoordinator.PrepareVoiceInputAsync(", body, StringComparison.Ordinal);
        Assert.DoesNotContain("_voiceInputService.IsReady", body, StringComparison.Ordinal);
        Assert.DoesNotContain("_voiceInputService.PrepareAsync(", body, StringComparison.Ordinal);
    }

    [Fact]
    public void PushToTalkWakeWordAndTtsMethods_RemainUnmigrated()
    {
        // Fuera de alcance de 1.3B1: deben seguir usando los tres campos directos, no el
        // coordinador. Se verifica dentro del cuerpo de cada método para no depender de
        // números de línea — solo de las firmas, que son símbolos estables.
        var content = ReadMainWindowSource();

        var voiceInputStarted = ExtractMethodBody(
            content,
            "private async void AssistantView_VoiceInputStarted",
            "private async void AssistantView_VoiceInputStopped");
        Assert.Contains("await _voiceInputService.StartListeningAsync();", voiceInputStarted, StringComparison.Ordinal);

        var voiceInputStopped = ExtractMethodBody(
            content,
            "private async void AssistantView_VoiceInputStopped",
            "private void WakeWordService_WakeWordDetected(");
        Assert.Contains("await _voiceInputService.StopListeningAsync();", voiceInputStopped, StringComparison.Ordinal);

        var handleWakeWordDetected = ExtractMethodBody(
            content,
            "private async Task HandleWakeWordDetectedAsync",
            "private async Task HandleVoiceRecognitionResultAsync");
        Assert.Contains(
            "await _voiceInputService.ListenForUtteranceAsync(",
            handleWakeWordDetected,
            StringComparison.Ordinal);

        var speakVoiceResult = ExtractMethodBody(content, "private void SpeakVoiceResult", endMarker: null);
        Assert.Contains("_voiceOutputService.SpeakShort(text);", speakVoiceResult, StringComparison.Ordinal);

        // Suscripción directa a los eventos del servicio, cableada en el constructor:
        // todavía no pasa por los accessors de paso directo de VoiceCoordinator.
        Assert.Contains(
            "_wakeWordService.WakeWordDetected += WakeWordService_WakeWordDetected;",
            content,
            StringComparison.Ordinal);
    }

    private static string ReadMainWindowSource() =>
        File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Nexo.App", "MainWindow.xaml.cs"));

    private static string ReadAppSource() =>
        File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Nexo.App", "App.xaml.cs"));

    private static string ExtractMethodBody(string content, string startMarker, string? endMarker)
    {
        var start = content.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"No se encontró '{startMarker}' en el archivo.");

        if (endMarker is null)
        {
            return content[start..];
        }

        var end = content.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, $"No se encontró '{endMarker}' después de '{startMarker}'.");
        return content[start..end];
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
