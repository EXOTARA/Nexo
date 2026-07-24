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
        // Corregida en 1.3A.1: bajo la semántica de sesión persistente, un segundo
        // Start nunca progresa sin un Stop/Cancel intermedio — a diferencia de la
        // versión original de esta prueba (1.3A), que esperaba que ambos Start
        // completaran solos porque el candado se liberaba al final de cada Start. Esa
        // expectativa ya no representa el comportamiento corregido (ver
        // FirstStartCompletes_SecondStartWaitsUntilStop). Se conserva aquí el objetivo
        // original — demostrar que el servicio subyacente nunca ve dos
        // StartListeningAsync solapados — insertando el Stop que la corrección exige.
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
        await first;

        // `second` sigue bloqueada: la sesión de `first` sigue abierta hasta el Stop.
        Assert.False(second.IsCompleted);
        Assert.Equal(1, voiceInput.StartListeningCallCount);

        voiceInput.BeforeStartListeningReturns = null;
        await coordinator.StopPushToTalkAsync();
        await second;

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

    // ---------- 1.3A.1: sesión de push-to-talk persistente ----------

    [Fact]
    public async Task FirstStartCompletes_SecondStartWaitsUntilStop()
    {
        var (coordinator, _, voiceInput, _, _) = CreateCoordinator();

        var firstResult = await coordinator.StartPushToTalkAsync();
        Assert.True(firstResult.IsAvailable);
        Assert.Equal(1, voiceInput.StartListeningCallCount);

        var second = coordinator.StartPushToTalkAsync();

        // Determinista sin sleeps: si `second` ya hubiera adquirido el candado y
        // llamado al servicio, StartListeningCallCount sería 2 aquí. La sesión de
        // `first` sigue abierta (no se llamó Stop), así que `second` debe seguir
        // bloqueada esperando el candado.
        Assert.False(second.IsCompleted);
        Assert.Equal(1, voiceInput.StartListeningCallCount);

        await coordinator.StopPushToTalkAsync();
        var secondResult = await second;

        Assert.True(secondResult.IsAvailable);
        Assert.Equal(2, voiceInput.StartListeningCallCount);
    }

    [Fact]
    public async Task FirstStartCompletes_SecondStartWaitsUntilCancel()
    {
        var (coordinator, _, voiceInput, _, _) = CreateCoordinator();

        await coordinator.StartPushToTalkAsync();
        var second = coordinator.StartPushToTalkAsync();
        Assert.False(second.IsCompleted);

        await coordinator.CancelPushToTalkAsync();
        var secondResult = await second;

        Assert.True(secondResult.IsAvailable);
        Assert.Equal(1, voiceInput.CancelCallCount);
        Assert.Equal(2, voiceInput.StartListeningCallCount);
    }

    [Fact]
    public async Task SecondStart_DoesNotCallUnderlyingServiceWhileFirstSessionIsActive()
    {
        var (coordinator, _, voiceInput, _, _) = CreateCoordinator();

        await coordinator.StartPushToTalkAsync();
        Assert.Equal(1, voiceInput.StartListeningCallCount);

        var second = coordinator.StartPushToTalkAsync();

        Assert.Equal(1, voiceInput.StartListeningCallCount);
        Assert.False(second.IsCompleted);

        // Limpieza: cierra la sesión para no dejar una tarea colgada al final de la prueba.
        await coordinator.StopPushToTalkAsync();
        await second;
    }

    [Fact]
    public async Task Stop_ReleasesSessionExactlyOnce()
    {
        var (coordinator, _, voiceInput, _, _) = CreateCoordinator();
        await coordinator.StartPushToTalkAsync();

        await coordinator.StopPushToTalkAsync();
        Assert.Equal(1, voiceInput.StopListeningCallCount);

        // Un segundo Stop sin sesión activa no debe volver a llamar al servicio ni
        // intentar liberar el candado otra vez (eso lanzaría SemaphoreFullException).
        var secondStopResult = await coordinator.StopPushToTalkAsync();
        Assert.Equal(1, voiceInput.StopListeningCallCount);
        Assert.False(secondStopResult.IsRecognized);

        // El candado quedó libre exactamente una vez: una nueva sesión puede arrancar.
        var thirdStart = await coordinator.StartPushToTalkAsync();
        Assert.True(thirdStart.IsAvailable);
    }

    [Fact]
    public async Task Cancel_ReleasesSessionExactlyOnce()
    {
        var (coordinator, _, voiceInput, _, _) = CreateCoordinator();
        await coordinator.StartPushToTalkAsync();

        await coordinator.CancelPushToTalkAsync();
        Assert.Equal(1, voiceInput.CancelCallCount);

        await coordinator.CancelPushToTalkAsync();
        Assert.Equal(1, voiceInput.CancelCallCount);

        var secondStart = await coordinator.StartPushToTalkAsync();
        Assert.True(secondStart.IsAvailable);
    }

    [Fact]
    public async Task FailedStart_ReleasesSession()
    {
        var (coordinator, _, voiceInput, _, _) = CreateCoordinator();
        voiceInput.StartResult = VoiceStartResult.Unavailable("micrófono ocupado");

        var failed = await coordinator.StartPushToTalkAsync();
        Assert.False(failed.IsAvailable);

        // El arranque fallido nunca estableció sesión: el candado no quedó retenido.
        voiceInput.StartResult = VoiceStartResult.Started("fake");
        var succeeded = await coordinator.StartPushToTalkAsync();

        Assert.True(succeeded.IsAvailable);
        Assert.Equal(2, voiceInput.StartListeningCallCount);
    }

    [Fact]
    public async Task CancelledStart_ReleasesSession()
    {
        var (coordinator, _, voiceInput, _, _) = CreateCoordinator();
        using var cts = new CancellationTokenSource();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        voiceInput.BeforeStartListeningReturns = () => release.Task;

        // Cancela mientras la llamada está genuinamente en vuelo dentro del servicio
        // subyacente (retenida por `release`), no antes de empezar.
        var cancelledStart = coordinator.StartPushToTalkAsync(cts.Token);
        Assert.False(cancelledStart.IsCompleted);

        await cts.CancelAsync();
        release.SetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledStart);

        // La sesión nunca se estableció: una llamada posterior debe iniciar con normalidad.
        voiceInput.BeforeStartListeningReturns = null;
        var succeeded = await coordinator.StartPushToTalkAsync();
        Assert.True(succeeded.IsAvailable);
    }

    [Fact]
    public async Task ConcurrentStopAndCancel_DoNotDoubleRelease()
    {
        var (coordinator, _, voiceInput, _, _) = CreateCoordinator();
        await coordinator.StartPushToTalkAsync();

        var stopTask = coordinator.StopPushToTalkAsync();
        var cancelTask = coordinator.CancelPushToTalkAsync();

        var exception = await Record.ExceptionAsync(() => Task.WhenAll(stopTask, cancelTask));
        Assert.Null(exception);

        // Exactamente una de las dos operaciones subyacentes se ejecutó, nunca las dos:
        // Interlocked.Exchange decide un único ganador entre Stop y Cancel.
        var totalUnderlyingCalls = voiceInput.StopListeningCallCount + voiceInput.CancelCallCount;
        Assert.Equal(1, totalUnderlyingCalls);

        // El candado se liberó exactamente una vez: una nueva sesión puede arrancar sin
        // quedarse colgada (lo que probaría una doble adquisición o una fuga).
        var nextStart = await coordinator.StartPushToTalkAsync();
        Assert.True(nextStart.IsAvailable);
    }

    [Fact]
    public async Task SessionCanStartAgainAfterStop()
    {
        var (coordinator, _, voiceInput, _, _) = CreateCoordinator();
        await coordinator.StartPushToTalkAsync();
        await coordinator.StopPushToTalkAsync();

        var result = await coordinator.StartPushToTalkAsync();

        Assert.True(result.IsAvailable);
        Assert.Equal(2, voiceInput.StartListeningCallCount);
    }

    [Fact]
    public async Task SessionCanStartAgainAfterCancel()
    {
        var (coordinator, _, voiceInput, _, _) = CreateCoordinator();
        await coordinator.StartPushToTalkAsync();
        await coordinator.CancelPushToTalkAsync();

        var result = await coordinator.StartPushToTalkAsync();

        Assert.True(result.IsAvailable);
        Assert.Equal(2, voiceInput.StartListeningCallCount);
    }

    [Fact]
    public async Task WakeWordLockOrderRemainsVoiceThenWakeWord()
    {
        var (coordinator, log, _, _, _) = CreateCoordinator();

        await coordinator.StartPushToTalkAsync();

        Assert.Equal(
            ["wakeWord.stopListening", "voiceOutput.stop", "voiceInput.startListening"],
            log.Entries);

        // Con la sesión de push-to-talk todavía abierta (candado de voz retenido a
        // propósito), una operación que solo toca el candado de wake word no debe
        // bloquearse: ninguna ruta adquiere wake-word antes que voice, así que no hay
        // forma de que el candado de voz, ya retenido, bloquee a StartWakeWordAsync.
        var wakeWordStart = await coordinator.StartWakeWordAsync(WakeWordPhrase.OyeKohana);
        Assert.True(wakeWordStart.IsAvailable);

        await coordinator.StopPushToTalkAsync();
    }

    [Fact]
    public async Task Dispose_DoesNotDisposeUnderlyingServices()
    {
        var (coordinator, _, voiceInput, voiceOutput, wakeWord) = CreateCoordinator();
        await coordinator.StartPushToTalkAsync();

        coordinator.Dispose();

        Assert.False(voiceInput.WasDisposed);
        Assert.False(voiceOutput.WasDisposed);
        Assert.False(wakeWord.WasDisposed);
    }

    [Fact]
    public async Task Dispose_DoesNotRaceWithAnActiveOrWaitingSession()
    {
        var (coordinator, _, _, _, _) = CreateCoordinator();

        // Sesión activa: el candado de voz sigue retenido (a propósito) en este punto.
        await coordinator.StartPushToTalkAsync();

        // Dos Dispose() concurrentes sobre una sesión activa: ninguno debe lanzar, y
        // los dos SemaphoreSlim propios solo se liberan una vez
        // (Interlocked.CompareExchange decide un único ejecutor real). Esta prueba no
        // cubre el caso de una llamada bloqueada dentro de WaitAsync() en el instante
        // exacto de Dispose — ese es el límite conocido documentado en VoiceCoordinator.Dispose.
        var disposeOne = Task.Run(coordinator.Dispose);
        var disposeTwo = Task.Run(coordinator.Dispose);

        var exception = await Record.ExceptionAsync(() => Task.WhenAll(disposeOne, disposeTwo));
        Assert.Null(exception);
    }
}
