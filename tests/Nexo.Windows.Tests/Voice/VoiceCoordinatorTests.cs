using Nexo.Core.Voice;
using Nexo.Windows.Voice;

namespace Nexo.Windows.Tests.Voice;

public sealed class VoiceCoordinatorTests
{
    private static (VoiceCoordinator Coordinator, VoiceCallLog Log, FakeVoiceInputService VoiceInput,
        FakeVoiceOutputService VoiceOutput, FakeWakeWordService WakeWord) CreateCoordinator()
    {
        var log = new VoiceCallLog();
        var voiceInput = new FakeVoiceInputService(log);
        var voiceOutput = new FakeVoiceOutputService(log);
        var wakeWord = new FakeWakeWordService(log);
        var coordinator = new VoiceCoordinator(voiceInput, voiceOutput, wakeWord);
        return (coordinator, log, voiceInput, voiceOutput, wakeWord);
    }

    // ---------- Propiedad exacta de las tres dependencias ----------

    [Fact]
    public void Constructor_UsesExactlyTheThreeProvidedDependencies()
    {
        var (coordinator, _, voiceInput, voiceOutput, wakeWord) = CreateCoordinator();

        var devices = new[] { new VoiceInputDevice(7, "Micrófono de prueba") };
        voiceInput.Devices = devices;

        // Identidad, no un cuarto motor: lo que devuelve el coordinador es exactamente
        // lo que expone el fake que se le entregó por constructor.
        Assert.Same(devices, coordinator.GetInputDevices());

        coordinator.InputDeviceNumber = 3;
        Assert.Equal(3, voiceInput.InputDeviceNumber);
        Assert.Equal(3, wakeWord.InputDeviceNumber);

        coordinator.WakeWordSensitivity = WakeWordSensitivity.High;
        Assert.Equal(WakeWordSensitivity.High, wakeWord.Sensitivity);

        IReadOnlyList<string> aliases = ["mi asistente"];
        coordinator.WakeWordCustomAliases = aliases;
        Assert.Same(aliases, wakeWord.CustomAliases);

        coordinator.Speak("hola");
        Assert.Equal(1, voiceOutput.SpeakShortCallCount);
        Assert.Equal("hola", voiceOutput.LastSpokenText);
    }

    [Fact]
    public void Constructor_RejectsNullDependencies_InsteadOfCreatingReplacements()
    {
        var log = new VoiceCallLog();
        var voiceInput = new FakeVoiceInputService(log);
        var voiceOutput = new FakeVoiceOutputService(log);
        var wakeWord = new FakeWakeWordService(log);

        Assert.Throws<ArgumentNullException>(() =>
            new VoiceCoordinator(null!, voiceOutput, wakeWord));
        Assert.Throws<ArgumentNullException>(() =>
            new VoiceCoordinator(voiceInput, null!, wakeWord));
        Assert.Throws<ArgumentNullException>(() =>
            new VoiceCoordinator(voiceInput, voiceOutput, null!));
    }

    // ---------- Reenvío de eventos ----------

    [Fact]
    public void WakeWordDetected_ForwardsFromUnderlyingService()
    {
        var (coordinator, _, _, _, wakeWord) = CreateCoordinator();
        WakeWordDetectedEventArgs? received = null;
        coordinator.WakeWordDetected += (_, e) => received = e;

        var args = new WakeWordDetectedEventArgs(WakeWordPhrase.OyeKohana, "oye kohana");
        wakeWord.RaiseWakeWordDetected(args);

        Assert.Same(args, received);
    }

    [Fact]
    public void RecognitionObserved_ForwardsFromUnderlyingService()
    {
        var (coordinator, _, _, _, wakeWord) = CreateCoordinator();
        WakeWordRecognitionObservedEventArgs? received = null;
        coordinator.RecognitionObserved += (_, e) => received = e;

        var match = WakeWordMatchResult.Accepted("oye kohana", "oye kohana", WakeWordMatchKind.Exact, "ok");
        var args = new WakeWordRecognitionObservedEventArgs(WakeWordPhrase.OyeKohana, "oye kohana", true, match);
        wakeWord.RaiseRecognitionObserved(args);

        Assert.Same(args, received);
    }

    [Fact]
    public void WakeWordDetected_Unsubscribe_StopsForwarding()
    {
        var (coordinator, _, _, _, wakeWord) = CreateCoordinator();
        var callCount = 0;
        void Handler(object? _, WakeWordDetectedEventArgs e) => callCount++;

        coordinator.WakeWordDetected += Handler;
        coordinator.WakeWordDetected -= Handler;
        wakeWord.RaiseWakeWordDetected(new WakeWordDetectedEventArgs(WakeWordPhrase.Kohana, "kohana"));

        Assert.Equal(0, callCount);
    }

    // ---------- Orden de operaciones y de candados ----------

    [Fact]
    public async Task StartPushToTalkAsync_StopsWakeWordAndTtsBeforeStartingListening()
    {
        var (coordinator, log, _, _, _) = CreateCoordinator();

        await coordinator.StartPushToTalkAsync();

        Assert.Equal(
            ["wakeWord.stopListening", "voiceOutput.stop", "voiceInput.startListening"],
            log.Entries);
    }

    [Fact]
    public async Task ListenAfterWakeWordAsync_StopsWakeWordAndTtsBeforeListening()
    {
        var (coordinator, log, _, _, _) = CreateCoordinator();

        await coordinator.ListenAfterWakeWordAsync(
            TimeSpan.FromSeconds(20),
            TimeSpan.FromMilliseconds(1_500));

        Assert.Equal(
            ["wakeWord.stopListening", "voiceOutput.stop", "voiceInput.listenForUtterance"],
            log.Entries);
    }

    [Fact]
    public async Task PrepareThenStartPushToTalk_RunsInTheOrderTheCallerChose()
    {
        var (coordinator, log, voiceInput, _, _) = CreateCoordinator();

        await coordinator.PrepareVoiceInputAsync();
        await coordinator.StartPushToTalkAsync();

        Assert.Equal(1, voiceInput.PrepareCallCount);
        Assert.Equal(1, voiceInput.StartListeningCallCount);
        Assert.Equal(
            ["voiceInput.prepare", "wakeWord.stopListening", "voiceOutput.stop", "voiceInput.startListening"],
            log.Entries);
    }

    [Fact]
    public async Task StopWakeWordAsync_ThenStartWakeWordAsync_ResumesSuccessfully()
    {
        var (coordinator, _, _, _, wakeWord) = CreateCoordinator();

        await coordinator.StartWakeWordAsync(WakeWordPhrase.OyeKohana);
        Assert.True(coordinator.IsWakeWordListening);

        await coordinator.StopWakeWordAsync();
        Assert.False(coordinator.IsWakeWordListening);

        var result = await coordinator.StartWakeWordAsync(WakeWordPhrase.OyeKohana);

        Assert.True(result.IsAvailable);
        Assert.True(coordinator.IsWakeWordListening);
        Assert.Equal(2, wakeWord.StartListeningCallCount);
        Assert.Equal(1, wakeWord.StopListeningCallCount);
    }

    [Fact]
    public async Task PauseWakeWordAsync_IsTheSameMechanismAsStop()
    {
        var (coordinator, _, _, _, wakeWord) = CreateCoordinator();
        await coordinator.StartWakeWordAsync(WakeWordPhrase.Kohana);

        await coordinator.PauseWakeWordAsync();

        Assert.False(coordinator.IsWakeWordListening);
        Assert.Equal(1, wakeWord.StopListeningCallCount);
    }

    [Fact(Timeout = 5000)]
    public async Task ConcurrentStartWakeWordAndPushToTalk_DoNotDeadlock()
    {
        // El candado de voz envuelve al de wake word (nunca al revés); una llamada a
        // StartWakeWordAsync concurrente con StartPushToTalkAsync solo debe serializarse
        // en el candado de wake word, sin interbloqueo. Sin Task.Delay: si hay
        // interbloqueo, el límite de tiempo del propio [Fact] falla la prueba.
        var (coordinator, _, _, _, _) = CreateCoordinator();

        var pushToTalk = coordinator.StartPushToTalkAsync();
        var wakeWordStart = coordinator.StartWakeWordAsync(WakeWordPhrase.OyeKohana);

        await Task.WhenAll(pushToTalk, wakeWordStart);

        Assert.True(pushToTalk.IsCompletedSuccessfully && wakeWordStart.IsCompletedSuccessfully);
    }

    // ---------- Evitar escuchas simultáneas ----------

    [Fact]
    public async Task TwoSimultaneousPushToTalkCalls_NeverOverlapInTheUnderlyingService()
    {
        var (coordinator, _, voiceInput, _, _) = CreateCoordinator();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        voiceInput.BeforeStartListeningReturns = () => release.Task;

        var first = coordinator.StartPushToTalkAsync();
        var second = coordinator.StartPushToTalkAsync();

        // Sin ningún sleep: en el momento en que ambas llamadas devuelven el control
        // (justo después de suspenderse en sus respectivos `await`), ninguna puede
        // haber terminado — la primera está retenida por `release` dentro del propio
        // servicio, y la segunda sigue esperando el candado de voz que la primera tiene.
        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);

        release.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, voiceInput.MaxObservedConcurrentStartListeningCalls);
        Assert.Equal(2, voiceInput.StartListeningCallCount);
    }

    // ---------- Cancelación ----------

    [Fact]
    public async Task StartPushToTalkAsync_PreCancelledToken_NeverCallsTheUnderlyingServices()
    {
        var (coordinator, _, voiceInput, voiceOutput, wakeWord) = CreateCoordinator();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => coordinator.StartPushToTalkAsync(cts.Token));

        Assert.Equal(0, voiceInput.StartListeningCallCount);
        Assert.Equal(0, voiceOutput.StopCallCount);
        Assert.Equal(0, wakeWord.StopListeningCallCount);
    }

    [Fact]
    public async Task ListenAfterWakeWordAsync_CancelledMidway_DoesNotStartListening()
    {
        var (coordinator, _, voiceInput, _, wakeWord) = CreateCoordinator();
        using var cts = new CancellationTokenSource();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        wakeWord.BeforeStopListeningReturns = () => release.Task;

        // En el momento en que esta llamada devuelve el control, ya está suspendida
        // dentro de StopListeningAsync (retenida por `release`): es un punto de
        // cancelación real a mitad de operación, sin ningún Task.Delay.
        var task = coordinator.ListenAfterWakeWordAsync(
            TimeSpan.FromSeconds(20),
            TimeSpan.FromMilliseconds(1_500),
            cancellationToken: cts.Token);
        Assert.False(task.IsCompleted);

        await cts.CancelAsync();
        release.SetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(0, voiceInput.ListenForUtteranceCallCount);
    }

    // ---------- Propiedad y liberación ----------

    [Fact]
    public async Task Dispose_DoesNotDisposeTheUnderlyingServices()
    {
        var (coordinator, _, voiceInput, voiceOutput, wakeWord) = CreateCoordinator();
        await coordinator.StartWakeWordAsync(WakeWordPhrase.OyeKohana);

        coordinator.Dispose();

        Assert.False(voiceInput.WasDisposed);
        Assert.False(voiceOutput.WasDisposed);
        Assert.False(wakeWord.WasDisposed);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var (coordinator, _, _, _, _) = CreateCoordinator();

        var exception = Record.Exception(() =>
        {
            coordinator.Dispose();
            coordinator.Dispose();
        });

        Assert.Null(exception);
    }

    // ---------- Fase 1.3B2A: operaciones bajo coordinación externa ----------
    //
    // Estas operaciones son delegaciones transparentes sin candado. Las pruebas
    // verifican tres cosas: que delegan exactamente una vez, que preservan los
    // argumentos tal cual, y que NO añaden ningún paso (ni pausa de wake word, ni Stop
    // de TTS, ni preparación) que los métodos compuestos sí realizan.

    [Fact]
    public async Task StartVoiceInputUnderExternalCoordination_DelegatesExactlyOnce()
    {
        var (coordinator, log, voiceInput, _, _) = CreateCoordinator();
        using var cts = new CancellationTokenSource();

        var result = await coordinator.StartVoiceInputUnderExternalCoordinationAsync(cts.Token);

        Assert.Equal(1, voiceInput.StartListeningCallCount);
        Assert.Same(voiceInput.StartResult, result);
        Assert.Equal(cts.Token, voiceInput.LastStartListeningToken);
        Assert.Equal(["voiceInput.startListening"], log.Entries);
    }

    [Fact]
    public async Task StopVoiceInputUnderExternalCoordination_DelegatesExactlyOnce()
    {
        var (coordinator, log, voiceInput, _, _) = CreateCoordinator();
        using var cts = new CancellationTokenSource();

        var result = await coordinator.StopVoiceInputUnderExternalCoordinationAsync(cts.Token);

        Assert.Equal(1, voiceInput.StopListeningCallCount);
        Assert.Same(voiceInput.StopResult, result);
        Assert.Equal(cts.Token, voiceInput.LastStopListeningToken);
        Assert.Equal(["voiceInput.stopListening"], log.Entries);
    }

    [Fact]
    public async Task CancelVoiceInputUnderExternalCoordination_DelegatesExactlyOnce()
    {
        var (coordinator, log, voiceInput, _, _) = CreateCoordinator();

        await coordinator.CancelVoiceInputUnderExternalCoordinationAsync();

        Assert.Equal(1, voiceInput.CancelCallCount);
        Assert.Equal(["voiceInput.cancel"], log.Entries);
    }

    [Fact]
    public async Task ListenForUtteranceUnderExternalCoordination_PreservesEveryArgument()
    {
        var (coordinator, log, voiceInput, _, _) = CreateCoordinator();
        using var cts = new CancellationTokenSource();
        var maximumDuration = TimeSpan.FromSeconds(17);
        var trailingSilence = TimeSpan.FromMilliseconds(1_234);
        var preRoll = new byte[] { 1, 2, 3 };
        var postWake = new byte[] { 4, 5 };

        var result = await coordinator.ListenForUtteranceUnderExternalCoordinationAsync(
            maximumDuration,
            trailingSilence,
            preRoll,
            postWake,
            cts.Token);

        Assert.Equal(1, voiceInput.ListenForUtteranceCallCount);
        Assert.Same(voiceInput.ListenResult, result);
        Assert.Equal(maximumDuration, voiceInput.LastMaximumDuration);
        Assert.Equal(trailingSilence, voiceInput.LastTrailingSilence);
        Assert.Equal(preRoll, voiceInput.LastInitialPcmAudio.ToArray());
        Assert.Equal(postWake, voiceInput.LastInitialSpeechPcmAudio.ToArray());
        Assert.Equal(cts.Token, voiceInput.LastListenForUtteranceToken);
        Assert.Equal(["voiceInput.listenForUtterance"], log.Entries);
    }

    [Fact]
    public async Task StartWakeWordUnderExternalCoordination_PreservesPhraseAndToken()
    {
        var (coordinator, log, _, _, wakeWord) = CreateCoordinator();
        using var cts = new CancellationTokenSource();

        var result = await coordinator.StartWakeWordUnderExternalCoordinationAsync(
            WakeWordPhrase.HeyKohana,
            cts.Token);

        Assert.Equal(1, wakeWord.StartListeningCallCount);
        Assert.Same(wakeWord.StartResult, result);
        Assert.Equal(WakeWordPhrase.HeyKohana, wakeWord.LastStartPhrase);
        Assert.Equal(cts.Token, wakeWord.LastStartListeningToken);
        Assert.Equal(["wakeWord.startListening"], log.Entries);
    }

    [Fact]
    public async Task StopWakeWordUnderExternalCoordination_DelegatesExactlyOnce()
    {
        var (coordinator, log, _, _, wakeWord) = CreateCoordinator();

        await coordinator.StopWakeWordUnderExternalCoordinationAsync();

        Assert.Equal(1, wakeWord.StopListeningCallCount);
        Assert.Equal(["wakeWord.stopListening"], log.Entries);
    }

    [Fact]
    public async Task ExternallyCoordinatedOperations_AddNoTtsStopNoWakeWordPauseNoPreparation()
    {
        // Contraste explícito con StartPushToTalkAsync/ListenAfterWakeWordAsync, que sí
        // pausan wake word y detienen el TTS. Aquí esos pasos son responsabilidad del
        // orquestador, así que el coordinador no debe ejecutarlos.
        var (coordinator, log, voiceInput, voiceOutput, wakeWord) = CreateCoordinator();

        await coordinator.StartVoiceInputUnderExternalCoordinationAsync();
        await coordinator.StopVoiceInputUnderExternalCoordinationAsync();
        await coordinator.ListenForUtteranceUnderExternalCoordinationAsync(
            TimeSpan.FromSeconds(20),
            TimeSpan.FromMilliseconds(1_500));

        Assert.Equal(0, voiceOutput.StopCallCount);
        Assert.Equal(0, wakeWord.StopListeningCallCount);
        Assert.Equal(0, voiceInput.PrepareCallCount);
        Assert.Equal(0, wakeWord.PrepareCallCount);
        Assert.Equal(
            ["voiceInput.startListening", "voiceInput.stopListening", "voiceInput.listenForUtterance"],
            log.Entries);
    }

    [Fact]
    public async Task ExternallyCoordinatedStart_IsNotSerializedByTheCoordinatorsInternalGates()
    {
        // Determinista sin sleeps: `StartListeningAsync` del fake incrementa su contador
        // de concurrencia antes de suspenderse en el gancho. Al invocar dos veces de
        // forma consecutiva, ambas llamadas llegan al fake antes de que ninguna termine
        // — algo imposible con el compuesto StartPushToTalkAsync, cuya prueba
        // TwoSimultaneousPushToTalkCalls_NeverOverlapInTheUnderlyingService exige que el
        // máximo observado sea 1.
        var (coordinator, _, voiceInput, _, _) = CreateCoordinator();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        voiceInput.BeforeStartListeningReturns = () => release.Task;

        var first = coordinator.StartVoiceInputUnderExternalCoordinationAsync();
        var second = coordinator.StartVoiceInputUnderExternalCoordinationAsync();

        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);
        Assert.Equal(2, voiceInput.MaxObservedConcurrentStartListeningCalls);

        release.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(2, voiceInput.StartListeningCallCount);
    }
}
