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
}
