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

    // ---------- Ámbito de entrada de voz: delegación exacta ----------

    [Fact]
    public async Task VoiceInputScope_DelegatesEveryOperationExactlyOnceWithItsArguments()
    {
        var (coordinator, log, voiceInput, _, _) = CreateCoordinator();
        using var startCts = new CancellationTokenSource();
        using var stopCts = new CancellationTokenSource();
        using var listenCts = new CancellationTokenSource();
        var maximumDuration = TimeSpan.FromSeconds(17);
        var trailingSilence = TimeSpan.FromMilliseconds(1_234);
        var preRoll = new byte[] { 1, 2, 3 };
        var postWake = new byte[] { 4, 5 };

        await using (var scope = await coordinator.AcquireVoiceInputScopeAsync())
        {
            var startResult = await scope.StartListeningAsync(startCts.Token);
            var listenResult = await scope.ListenForUtteranceAsync(
                maximumDuration, trailingSilence, preRoll, postWake, listenCts.Token);
            var stopResult = await scope.StopListeningAsync(stopCts.Token);
            await scope.CancelAsync();

            Assert.Same(voiceInput.StartResult, startResult);
            Assert.Same(voiceInput.ListenResult, listenResult);
            Assert.Same(voiceInput.StopResult, stopResult);
        }

        Assert.Equal(1, voiceInput.StartListeningCallCount);
        Assert.Equal(1, voiceInput.ListenForUtteranceCallCount);
        Assert.Equal(1, voiceInput.StopListeningCallCount);
        Assert.Equal(1, voiceInput.CancelCallCount);
        Assert.Equal(startCts.Token, voiceInput.LastStartListeningToken);
        Assert.Equal(stopCts.Token, voiceInput.LastStopListeningToken);
        Assert.Equal(listenCts.Token, voiceInput.LastListenForUtteranceToken);
        Assert.Equal(maximumDuration, voiceInput.LastMaximumDuration);
        Assert.Equal(trailingSilence, voiceInput.LastTrailingSilence);
        Assert.Equal(preRoll, voiceInput.LastInitialPcmAudio.ToArray());
        Assert.Equal(postWake, voiceInput.LastInitialSpeechPcmAudio.ToArray());

        // El coordinador no añade pasos: ni pausa wake word, ni detiene TTS.
        Assert.Equal(
            ["voiceInput.startListening", "voiceInput.listenForUtterance", "voiceInput.stopListening", "voiceInput.cancel"],
            log.Entries);
    }

    [Fact]
    public async Task WakeWordScope_DelegatesEveryOperationExactlyOnceWithItsArguments()
    {
        var (coordinator, log, _, _, wakeWord) = CreateCoordinator();
        using var startCts = new CancellationTokenSource();

        await using (var scope = await coordinator.AcquireWakeWordScopeAsync())
        {
            var startResult = await scope.StartListeningAsync(WakeWordPhrase.HeyKohana, startCts.Token);
            await scope.StopListeningAsync();

            Assert.Same(wakeWord.StartResult, startResult);
        }

        Assert.Equal(1, wakeWord.StartListeningCallCount);
        Assert.Equal(1, wakeWord.StopListeningCallCount);
        Assert.Equal(WakeWordPhrase.HeyKohana, wakeWord.LastStartPhrase);
        Assert.Equal(startCts.Token, wakeWord.LastStartListeningToken);
        Assert.Equal(["wakeWord.startListening", "wakeWord.stopListening"], log.Entries);
    }

    // ---------- Exclusión real: un solo dominio por servicio ----------

    [Fact]
    public async Task TwoVoiceInputScopes_NeverOverlapInTheUnderlyingService()
    {
        // El único candado de entrada de voz serializa dos ámbitos concurrentes: mientras
        // el primero retiene el servicio (bloqueado en `release`), el segundo no puede
        // siquiera adquirir el ámbito. Determinista, sin sleeps.
        var (coordinator, _, voiceInput, _, _) = CreateCoordinator();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        voiceInput.BeforeStartListeningReturns = () => release.Task;

        async Task RunUnderScope()
        {
            await using var scope = await coordinator.AcquireVoiceInputScopeAsync();
            await scope.StartListeningAsync();
        }

        var first = RunUnderScope();
        var second = RunUnderScope();

        Assert.False(first.IsCompleted);
        Assert.False(second.IsCompleted);

        release.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, voiceInput.MaxObservedConcurrentStartListeningCalls);
        Assert.Equal(2, voiceInput.StartListeningCallCount);
    }

    [Fact]
    public async Task AcquireVoiceInputScope_BlocksASecondAcquisitionUntilTheFirstIsDisposed()
    {
        var (coordinator, _, _, _, _) = CreateCoordinator();

        var first = await coordinator.AcquireVoiceInputScopeAsync();
        var second = coordinator.AcquireVoiceInputScopeAsync();
        Assert.False(second.IsCompleted);

        await first.DisposeAsync();
        var secondScope = await second;
        Assert.NotNull(secondScope);
        await secondScope.DisposeAsync();
    }

    [Fact]
    public async Task VoiceAndWakeWordScopes_AreIndependentDomains()
    {
        // Adquirir el ámbito de voz no impide adquirir el de wake word: son dos dominios
        // distintos, y el orden voz→wake word se puede anidar sin interbloqueo.
        var (coordinator, _, _, _, _) = CreateCoordinator();

        await using var voiceScope = await coordinator.AcquireVoiceInputScopeAsync();
        var wakeWordAcquisition = coordinator.AcquireWakeWordScopeAsync();

        Assert.True(wakeWordAcquisition.IsCompleted);
        await using var wakeWordScope = await wakeWordAcquisition;
        Assert.NotNull(wakeWordScope);
    }

    [Fact(Timeout = 5000)]
    public async Task NestedVoiceThenWakeWordScopes_DoNotDeadlock()
    {
        var (coordinator, _, _, _, _) = CreateCoordinator();

        await using var voiceScope = await coordinator.AcquireVoiceInputScopeAsync();
        await voiceScope.StartListeningAsync();
        await using (var wakeWordScope = await coordinator.AcquireWakeWordScopeAsync())
        {
            await wakeWordScope.StopListeningAsync();
        }

        Assert.True(coordinator.IsVoiceInputListening);
    }

    // ---------- Liberación exacta del ámbito ----------

    [Fact]
    public async Task DisposingAScopeTwice_ReleasesTheGateOnlyOnce()
    {
        // Si el DisposeAsync liberara dos veces, un tercer intento de adquirir dispararía
        // SemaphoreFullException (o dejaría pasar dos titulares). Ninguna de las dos cosas
        // debe ocurrir: doble Dispose es inocuo y la exclusión sigue intacta.
        var (coordinator, _, _, _, _) = CreateCoordinator();

        var scope = await coordinator.AcquireVoiceInputScopeAsync();
        await scope.DisposeAsync();
        var exception = await Record.ExceptionAsync(async () => await scope.DisposeAsync());
        Assert.Null(exception);

        // El candado quedó libre exactamente una vez: se puede readquirir, y una segunda
        // adquisición concurrente sigue bloqueada (no hay dos permisos).
        var reacquired = await coordinator.AcquireVoiceInputScopeAsync();
        var blocked = coordinator.AcquireVoiceInputScopeAsync();
        Assert.False(blocked.IsCompleted);

        await reacquired.DisposeAsync();
        await (await blocked).DisposeAsync();
    }

    // ---------- Cancelación ----------

    [Fact]
    public async Task AcquireVoiceInputScope_PreCancelledToken_ThrowsAndDoesNotHoldTheGate()
    {
        var (coordinator, _, _, _, _) = CreateCoordinator();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => coordinator.AcquireVoiceInputScopeAsync(cts.Token));

        // El candado no quedó tomado: una adquisición posterior completa de inmediato.
        var scope = coordinator.AcquireVoiceInputScopeAsync();
        Assert.True(scope.IsCompleted);
        await (await scope).DisposeAsync();
    }

    [Fact]
    public async Task AcquireWakeWordScope_PreCancelledToken_ThrowsAndDoesNotHoldTheGate()
    {
        var (coordinator, _, _, _, _) = CreateCoordinator();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => coordinator.AcquireWakeWordScopeAsync(cts.Token));

        var scope = coordinator.AcquireWakeWordScopeAsync();
        Assert.True(scope.IsCompleted);
        await (await scope).DisposeAsync();
    }

    // ---------- Propiedad y liberación ----------

    [Fact]
    public async Task Dispose_DoesNotDisposeTheUnderlyingServices()
    {
        var (coordinator, _, voiceInput, voiceOutput, wakeWord) = CreateCoordinator();
        await using (var scope = await coordinator.AcquireWakeWordScopeAsync())
        {
            await scope.StartListeningAsync(WakeWordPhrase.OyeKohana);
        }

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
