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
    public void ChangeVoiceInputDeviceAsync_UsesTheCoordinatorForEveryVoiceOperation()
    {
        // Fase 1.3B3: el candado de entrada de voz se sostiene con un ámbito del
        // coordinador y la cancelación corre sobre ese ámbito. MainWindow ya no tiene
        // semáforo de voz propio.
        var body = ExtractMethodBody(
            ReadMainWindowSource(),
            "private async Task ChangeVoiceInputDeviceAsync",
            "private async Task<bool> TryHandlePendingVoiceDecisionAsync(");

        Assert.Contains(
            "await using var voiceScope = await _voiceCoordinator.AcquireVoiceInputScopeAsync();",
            body,
            StringComparison.Ordinal);
        Assert.DoesNotContain("_voiceGate", body, StringComparison.Ordinal);
        Assert.Contains("await PauseWakeWordAsync();", body, StringComparison.Ordinal);
        Assert.Contains("await ResumeWakeWordIfEnabledAsync();", body, StringComparison.Ordinal);
        Assert.Contains("await voiceScope.CancelAsync();", body, StringComparison.Ordinal);
        Assert.Contains("_voiceCoordinator.InputDeviceNumber = deviceNumber;", body, StringComparison.Ordinal);
        // `.GetInputDevices()` se llama encadenado en varias líneas; se comprueba por
        // separado para no depender del formato exacto del encadenamiento.
        Assert.Contains(".GetInputDevices()", body, StringComparison.Ordinal);
        Assert.Contains("_voiceCoordinator.IsVoiceInputReady", body, StringComparison.Ordinal);
        Assert.DoesNotContain("_voiceInputService", body, StringComparison.Ordinal);
        Assert.DoesNotContain("_wakeWordService", body, StringComparison.Ordinal);
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

    // ---------- Fase 1.3B2 runtime: push-to-talk y wake-word-listen migrados ----------

    [Fact]
    public void AssistantViewVoiceInputStarted_PreservesOrchestrationAndUsesTheCoordinator()
    {
        var body = ExtractMethodBody(
            ReadMainWindowSource(),
            "private async void AssistantView_VoiceInputStarted",
            "private async void AssistantView_VoiceInputStopped");

        // Conservado exactamente: exclusión (ahora por ámbito), orquestación, bandera.
        Assert.Contains(
            "await using var voiceScope = await _voiceCoordinator.AcquireVoiceInputScopeAsync();",
            body,
            StringComparison.Ordinal);
        Assert.DoesNotContain("_voiceGate", body, StringComparison.Ordinal);
        Assert.Contains("await PauseWakeWordAsync();", body, StringComparison.Ordinal);
        Assert.Contains("await PrepareVoiceAsync();", body, StringComparison.Ordinal);
        Assert.Contains("listeningStarted", body, StringComparison.Ordinal);
        Assert.Contains("await ResumeWakeWordIfEnabledAsync();", body, StringComparison.Ordinal);

        // Migrado: TTS y disponibilidad por el coordinador, arranque por el ámbito.
        Assert.Contains("_voiceCoordinator.StopSpeaking();", body, StringComparison.Ordinal);
        Assert.Contains("_voiceCoordinator.IsVoiceInputReady", body, StringComparison.Ordinal);
        Assert.Contains("await voiceScope.StartListeningAsync();", body, StringComparison.Ordinal);

        // Ningún uso directo restante de los servicios en este método.
        Assert.DoesNotContain("_voiceInputService", body, StringComparison.Ordinal);
        Assert.DoesNotContain("_voiceOutputService", body, StringComparison.Ordinal);
    }

    [Fact]
    public void AssistantViewVoiceInputStopped_PreservesOrchestrationAndUsesTheCoordinator()
    {
        var body = ExtractMethodBody(
            ReadMainWindowSource(),
            "private async void AssistantView_VoiceInputStopped",
            "private void WakeWordService_WakeWordDetected(");

        Assert.Contains(
            "await using var voiceScope = await _voiceCoordinator.AcquireVoiceInputScopeAsync();",
            body,
            StringComparison.Ordinal);
        Assert.DoesNotContain("_voiceGate", body, StringComparison.Ordinal);
        Assert.Contains("_voiceCoordinator.IsVoiceInputListening", body, StringComparison.Ordinal);
        Assert.Contains("await voiceScope.StopListeningAsync();", body, StringComparison.Ordinal);
        Assert.Contains("await HandleVoiceRecognitionResultAsync(result);", body, StringComparison.Ordinal);

        // La reanudación en el finally de Stop es incondicional (a diferencia de Start,
        // que la condiciona a `!listeningStarted`): no debe aparecer ningún `if` entre el
        // bloque `finally` y la llamada a ResumeWakeWordIfEnabledAsync.
        var finallyIndex = body.IndexOf("finally", StringComparison.Ordinal);
        Assert.True(finallyIndex >= 0, "No se encontró el bloque finally.");
        var finallyBlock = body[finallyIndex..];
        var resumeIndex = finallyBlock.IndexOf("ResumeWakeWordIfEnabledAsync", StringComparison.Ordinal);
        Assert.True(resumeIndex >= 0, "No se encontró la reanudación en el finally.");
        Assert.DoesNotContain("if", finallyBlock[..resumeIndex], StringComparison.Ordinal);

        Assert.DoesNotContain("_voiceInputService", body, StringComparison.Ordinal);
    }

    [Fact]
    public void HandleWakeWordDetectedAsync_PreservesOrchestrationAndUsesTheCoordinator()
    {
        var body = ExtractMethodBody(
            ReadMainWindowSource(),
            "private async Task HandleWakeWordDetectedAsync",
            "private async Task HandleVoiceRecognitionResultAsync");

        // Conservado exactamente.
        Assert.Contains("RememberForegroundWindow();", body, StringComparison.Ordinal);
        Assert.Contains(
            "await using var voiceScope = await _voiceCoordinator.AcquireVoiceInputScopeAsync();",
            body,
            StringComparison.Ordinal);
        Assert.DoesNotContain("_voiceGate", body, StringComparison.Ordinal);
        Assert.Contains("await PauseWakeWordAsync();", body, StringComparison.Ordinal);
        Assert.Contains("await PrepareVoiceAsync();", body, StringComparison.Ordinal);
        Assert.Contains("TimeSpan.FromSeconds(20)", body, StringComparison.Ordinal);
        Assert.Contains("TimeSpan.FromMilliseconds(1_500)", body, StringComparison.Ordinal);
        Assert.Contains("e.PreRollAudio", body, StringComparison.Ordinal);
        Assert.Contains("e.PostWakeAudio", body, StringComparison.Ordinal);
        Assert.Contains("_lifetimeCancellation.Token", body, StringComparison.Ordinal);
        Assert.Contains("await ResumeWakeWordIfEnabledAsync();", body, StringComparison.Ordinal);

        // Migrado.
        Assert.Contains("_voiceCoordinator.StopSpeaking();", body, StringComparison.Ordinal);
        Assert.Contains("_voiceCoordinator.IsVoiceInputReady", body, StringComparison.Ordinal);
        Assert.Contains("await voiceScope.ListenForUtteranceAsync(", body, StringComparison.Ordinal);

        Assert.DoesNotContain("_voiceInputService", body, StringComparison.Ordinal);
        Assert.DoesNotContain("_voiceOutputService", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyWakeWordPreferenceAsync_PreservesOrderAndUsesTheCoordinator()
    {
        var body = ExtractMethodBody(
            ReadMainWindowSource(),
            "private async Task ApplyWakeWordPreferenceAsync(bool showCapsule)",
            "private async Task PauseWakeWordAsync()");

        // Sincronización final (1.3B3): el único candado de wake word vive en el
        // coordinador y se sostiene mediante un ámbito; MainWindow ya no tiene semáforo
        // de wake word propio. La sección crítica conserva la misma duración.
        Assert.Contains(
            "await using var wakeWordScope = await _voiceCoordinator.AcquireWakeWordScopeAsync();",
            body,
            StringComparison.Ordinal);
        Assert.DoesNotContain("_wakeWordGate", body, StringComparison.Ordinal);
        Assert.Contains("SetWakeWordIndicator(", body, StringComparison.Ordinal);
        Assert.Contains("RefreshRuntimeDashboard();", body, StringComparison.Ordinal);
        Assert.Contains("PauseWakeWordInGameMode", body, StringComparison.Ordinal);
        Assert.Contains("requiresDownload", body, StringComparison.Ordinal);
        Assert.Contains("Progress<VoicePreparationProgress>", body, StringComparison.Ordinal);
        Assert.Contains("_lifetimeCancellation.Token", body, StringComparison.Ordinal);
        Assert.Contains("catch (OperationCanceledException)", body, StringComparison.Ordinal);

        Assert.Contains("await wakeWordScope.StopListeningAsync();", body, StringComparison.Ordinal);
        Assert.Contains("_voiceCoordinator.PrepareWakeWordAsync(", body, StringComparison.Ordinal);
        Assert.Contains("await wakeWordScope.StartListeningAsync(", body, StringComparison.Ordinal);
        Assert.Contains("_voiceCoordinator.WakeWordSensitivity", body, StringComparison.Ordinal);
        Assert.Contains("_voiceCoordinator.WakeWordCustomAliases", body, StringComparison.Ordinal);
        Assert.Contains("_voiceCoordinator.IsVoiceInputReady", body, StringComparison.Ordinal);
        Assert.Contains("_voiceCoordinator.IsVoiceInputListening", body, StringComparison.Ordinal);
        Assert.Contains("_voiceCoordinator.IsWakeWordReady", body, StringComparison.Ordinal);

        // Orden Stop → Prepare → Start conservado.
        var stopIndex = body.IndexOf("wakeWordScope.StopListeningAsync", StringComparison.Ordinal);
        var prepareIndex = body.IndexOf("PrepareWakeWordAsync", StringComparison.Ordinal);
        var startIndex = body.IndexOf("wakeWordScope.StartListeningAsync", StringComparison.Ordinal);
        Assert.True(
            stopIndex >= 0 && prepareIndex > stopIndex && startIndex > prepareIndex,
            "El orden Stop -> Prepare -> Start cambió en ApplyWakeWordPreferenceAsync.");

        Assert.DoesNotContain("_wakeWordService", body, StringComparison.Ordinal);
        Assert.DoesNotContain("_voiceInputService", body, StringComparison.Ordinal);
    }

    [Fact]
    public void PauseWakeWordAsync_PreservesTheGateAndTheIndicator()
    {
        var body = ExtractMethodBody(
            ReadMainWindowSource(),
            "private async Task PauseWakeWordAsync()",
            "private Task ResumeWakeWordIfEnabledAsync()");

        Assert.Contains(
            "await using var wakeWordScope = await _voiceCoordinator.AcquireWakeWordScopeAsync();",
            body,
            StringComparison.Ordinal);
        Assert.DoesNotContain("_wakeWordGate", body, StringComparison.Ordinal);
        Assert.Contains("SetWakeWordIndicator(active: false);", body, StringComparison.Ordinal);
        Assert.Contains("await wakeWordScope.StopListeningAsync();", body, StringComparison.Ordinal);
        Assert.DoesNotContain("_wakeWordService", body, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_HasNoRemainingDirectOperationalCallsOnTheThreeServices()
    {
        // Tras la migración runtime de 1.3B2, los únicos usos directos permitidos de los
        // tres campos de servicio son: asignación/fallback del constructor, suscripción y
        // desuscripción de los dos eventos de wake word, y Dispose() en Window_Closed
        // (todos verificados por otras pruebas de esta clase). Ninguna llamada operativa
        // debe seguir apareciendo pegada a los campos directos.
        var content = ReadMainWindowSource();

        string[] forbiddenDirectUses =
        [
            "_voiceInputService.StartListeningAsync",
            "_voiceInputService.StopListeningAsync",
            "_voiceInputService.ListenForUtteranceAsync",
            "_voiceInputService.CancelAsync",
            "_voiceInputService.PrepareAsync",
            "_voiceInputService.IsReady",
            "_voiceInputService.IsListening",
            "_voiceInputService.GetInputDevices",
            "_voiceInputService.InputDeviceNumber",
            "_voiceOutputService.SpeakShort",
            "_voiceOutputService.Stop(",
            "_wakeWordService.StartListeningAsync",
            "_wakeWordService.StopListeningAsync",
            "_wakeWordService.PrepareAsync",
            "_wakeWordService.IsReady",
            "_wakeWordService.IsListening",
            "_wakeWordService.Sensitivity",
            "_wakeWordService.CustomAliases",
            "_wakeWordService.InputDeviceNumber"
        ];

        Assert.All(
            forbiddenDirectUses,
            use => Assert.DoesNotContain(use, content, StringComparison.Ordinal));
    }

    // ---------- Fase 1.3B2A/1.3B2: frontera de la API de transición ----------

    [Fact]
    public void MainWindow_NoLongerUsesTheTransitionalCoordinationApi()
    {
        // Sustituye al invariante de 1.3B2 "cada operación externa solo en su método
        // aprobado": desde 1.3B3 MainWindow ya no consume ninguna operación
        // …UnderExternalCoordinationAsync — todas las mutaciones de voz y wake word
        // corren sobre los ámbitos del coordinador. La API de transición se retira por
        // completo del coordinador en el commit de limpieza.
        var content = ReadMainWindowSource();

        Assert.DoesNotContain("UnderExternalCoordinationAsync", content, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_DoesNotConsumeTheCoordinatorsGateAcquiringOperations()
    {
        // Invariante de seguridad de toda la fase 1.3B2: mientras MainWindow conserve sus
        // propios semáforos, consumir un método compuesto del coordinador crearía un
        // segundo dominio de exclusión sobre el mismo servicio. Esta prueba lo impide
        // técnicamente, no solo por convención.
        var content = ReadMainWindowSource();

        string[] gateAcquiring =
        [
            "_voiceCoordinator.StartPushToTalkAsync",
            "_voiceCoordinator.StopPushToTalkAsync",
            "_voiceCoordinator.CancelPushToTalkAsync",
            "_voiceCoordinator.ListenAfterWakeWordAsync",
            "_voiceCoordinator.StartWakeWordAsync",
            "_voiceCoordinator.StopWakeWordAsync",
            "_voiceCoordinator.PauseWakeWordAsync"
        ];

        Assert.All(
            gateAcquiring,
            member => Assert.DoesNotContain(member, content, StringComparison.Ordinal));
    }

    [Fact]
    public void MainWindow_NoLongerOwnsTheVoiceOrWakeWordSemaphores()
    {
        // Fase 1.3B3: los candados de entrada de voz y de wake word se transfirieron al
        // coordinador (un único SemaphoreSlim por dominio, sostenido por ámbito).
        // MainWindow ya no declara `_voiceGate` ni `_wakeWordGate`. El único semáforo que
        // permanece en MainWindow serializa decisiones del Resource Governor (no es un
        // candado del motor de voz) y se comprueba aparte.
        var content = ReadMainWindowSource();

        Assert.DoesNotContain("_voiceGate", content, StringComparison.Ordinal);
        Assert.DoesNotContain("_wakeWordGate", content, StringComparison.Ordinal);
    }

    [Fact]
    public void ResourceGovernorSemaphore_IsNamedAsADecisionGate_NotAVoiceEngineGate()
    {
        // Fase 1.3B3: el semáforo del Resource Governor serializa decisiones (pausar /
        // reanudar wake word), no el motor. Se renombró para que su nombre no sugiera
        // falsamente que es un candado de voz; el acceso real a Vosk sigue pasando por el
        // ámbito de wake word del coordinador.
        var content = ReadMainWindowSource();

        Assert.Contains(
            "private readonly SemaphoreSlim _resourceGovernorDecisionGate = new(1, 1);",
            content,
            StringComparison.Ordinal);
        Assert.DoesNotContain("_resourceGovernorVoiceGate", content, StringComparison.Ordinal);

        // La rama del governor que pausa Vosk lo hace a través del método de MainWindow
        // que adquiere el ámbito de wake word, no tocando el servicio directamente.
        var body = ExtractMethodBody(
            content,
            "await _resourceGovernorDecisionGate.WaitAsync();",
            "private void UpdateResourceModeIndicator(");
        Assert.Contains("await PauseWakeWordAsync();", body, StringComparison.Ordinal);
        Assert.Contains("await ResumeWakeWordIfEnabledAsync();", body, StringComparison.Ordinal);
        Assert.DoesNotContain("_wakeWordService", body, StringComparison.Ordinal);
    }

    [Fact]
    public void VoiceCoordinator_ExternallyCoordinatedOperationsDoNotTouchTheInternalGates()
    {
        // Cada operación …UnderExternalCoordinationAsync es un miembro con cuerpo de
        // expresión. Se extrae exactamente desde su firma hasta el `;` que la cierra —
        // no hasta el miembro siguiente — para no incluir el XML-doc del método
        // posterior, que sí menciona los candados al documentar que no los usa.
        var content = ReadVoiceCoordinatorSource();

        string[] signatures =
        [
            "public Task<VoiceStartResult> StartVoiceInputUnderExternalCoordinationAsync",
            "public Task<VoiceRecognitionResult> StopVoiceInputUnderExternalCoordinationAsync",
            "public Task CancelVoiceInputUnderExternalCoordinationAsync",
            "public Task<VoiceRecognitionResult> ListenForUtteranceUnderExternalCoordinationAsync",
            "public Task<VoiceStartResult> StartWakeWordUnderExternalCoordinationAsync",
            "public Task StopWakeWordUnderExternalCoordinationAsync"
        ];

        foreach (var signature in signatures)
        {
            var body = ExtractExpressionBodiedMember(content, signature);
            Assert.DoesNotContain("_voiceGate", body, StringComparison.Ordinal);
            Assert.DoesNotContain("_wakeWordGate", body, StringComparison.Ordinal);
            Assert.DoesNotContain("WaitAsync", body, StringComparison.Ordinal);
            Assert.DoesNotContain("Release()", body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void VoiceCoordinator_PrivateGateHelperUsesTheDisambiguatedName()
    {
        // El sufijo "Core" quedó prohibido por ambigüedad: el helper privado SÍ adquiere
        // el candado, mientras que las operaciones nuevas NO lo hacen.
        var content = ReadVoiceCoordinatorSource();

        Assert.Contains(
            "private async Task StopWakeWordWithinCoordinatorGateAsync()",
            content,
            StringComparison.Ordinal);
        Assert.DoesNotContain("StopWakeWordCoreAsync", content, StringComparison.Ordinal);
    }

    /// <summary>
    /// Devuelve la declaración de un miembro con cuerpo de expresión, desde su firma
    /// hasta el `;` que lo termina. Estos miembros no contienen `;` en su lista de
    /// parámetros, así que el primer `;` posterior a la firma cierra siempre el cuerpo.
    /// </summary>
    private static string ExtractExpressionBodiedMember(string content, string signature)
    {
        var start = content.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"No se encontró '{signature}' en el archivo.");

        var end = content.IndexOf(';', start);
        Assert.True(end > start, $"No se encontró el final del cuerpo de '{signature}'.");
        return content[start..(end + 1)];
    }

    /// <summary>
    /// Verifica que un miembro solo aparezca dentro del rango [<paramref name="startMarker"/>,
    /// <paramref name="endMarker"/>) del archivo, y en ningún otro punto. Cuenta ocurrencias
    /// en vez de solo comprobar presencia, para detectar un segundo uso fuera de rango
    /// aunque también exista uno dentro.
    /// </summary>
    private static void AssertMemberAppearsOnlyWithin(
        string content,
        string member,
        string startMarker,
        string endMarker)
    {
        var body = ExtractMethodBody(content, startMarker, endMarker);
        var occurrencesInBody = CountOccurrences(body, member);
        var occurrencesInFile = CountOccurrences(content, member);

        Assert.True(occurrencesInBody > 0, $"'{member}' no se encontró dentro de '{startMarker}'.");
        Assert.Equal(occurrencesInFile, occurrencesInBody);
    }

    private static int CountOccurrences(string content, string substring)
    {
        var count = 0;
        var index = 0;
        while ((index = content.IndexOf(substring, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += substring.Length;
        }

        return count;
    }

    private static string ReadVoiceCoordinatorSource() =>
        File.ReadAllText(Path.Combine(
            RepositoryRoot, "src", "Nexo.Windows", "Voice", "VoiceCoordinator.cs"));

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
