using Nexo.Core.Voice;

namespace Nexo.Windows.Voice;

/// <summary>
/// Mecánica reutilizable de voz (push-to-talk y wake word) por encima de
/// <see cref="IVoiceInputService"/>, <see cref="IVoiceOutputService"/> e
/// <see cref="IWakeWordService"/>. Subfase 1.3A: existe de forma aislada, sin que la
/// vista principal de la aplicación lo consuma todavía.
///
/// Propiedad: este coordinador **no es dueño** del ciclo de vida de los tres servicios
/// que recibe. No los libera nunca; quien los construyó (hoy la vista principal, a través
/// de <c>KohanaCompositionRoot</c>) sigue siendo responsable de liberarlos, en el mismo
/// orden en que ya lo hace al cerrarse. <see cref="Dispose"/> aquí solo libera los dos
/// <see cref="SemaphoreSlim"/> que este tipo crea para sí mismo.
///
/// No depende de infraestructura de interfaz de usuario, de la ventana principal de la
/// aplicación, ni de las preferencias guardadas o la decisión del gobernador de recursos:
/// toda decisión de política (frase activa, modo juego, etc.) la sigue tomando quien
/// llame a este coordinador.
/// </summary>
public sealed class VoiceCoordinator : IDisposable
{
    /// <summary>
    /// Token opaco que representa una sesión de push-to-talk abierta (desde que
    /// <see cref="StartPushToTalkAsync"/> tiene éxito hasta que
    /// <see cref="StopPushToTalkAsync"/> o <see cref="CancelPushToTalkAsync"/> la
    /// cierra). No lleva datos: solo distingue "hay sesión" (no nulo) de "no hay
    /// sesión" (nulo) para <see cref="Interlocked.Exchange{T}(ref T, T)"/>.
    /// </summary>
    private sealed class PushToTalkSession
    {
    }

    private readonly IVoiceInputService _voiceInputService;
    private readonly IVoiceOutputService _voiceOutputService;
    private readonly IWakeWordService _wakeWordService;

    // Recursos propios del coordinador (no los tres servicios): se crean y se liberan aquí.
    private readonly SemaphoreSlim _voiceGate = new(1, 1);
    private readonly SemaphoreSlim _wakeWordGate = new(1, 1);

    /// <summary>
    /// Sesión de push-to-talk activa, o <c>null</c> si no hay ninguna. Se instala solo
    /// tras un <see cref="StartPushToTalkAsync"/> exitoso; solo <see cref="StopPushToTalkAsync"/>
    /// o <see cref="CancelPushToTalkAsync"/> (quien gane la carrera vía
    /// <see cref="Interlocked.Exchange{T}(ref T, T)"/>) la retira y libera <see cref="_voiceGate"/>.
    /// </summary>
    private PushToTalkSession? _activeSession;

    // 0 = no liberado, 1 = liberado. Interlocked en vez de bool: Dispose() debe ser
    // seguro si dos llamadas concurrentes lo invocan a la vez.
    private int _disposed;

    public VoiceCoordinator(
        IVoiceInputService voiceInputService,
        IVoiceOutputService voiceOutputService,
        IWakeWordService wakeWordService)
    {
        _voiceInputService = voiceInputService ?? throw new ArgumentNullException(nameof(voiceInputService));
        _voiceOutputService = voiceOutputService ?? throw new ArgumentNullException(nameof(voiceOutputService));
        _wakeWordService = wakeWordService ?? throw new ArgumentNullException(nameof(wakeWordService));
    }

    /// <summary>
    /// Paso directo al evento del servicio subyacente: no hay suscripción interna que
    /// limpiar, así que <see cref="Dispose"/> no necesita desuscribirse de nada aquí.
    /// </summary>
    public event EventHandler<WakeWordDetectedEventArgs>? WakeWordDetected
    {
        add => _wakeWordService.WakeWordDetected += value;
        remove => _wakeWordService.WakeWordDetected -= value;
    }

    /// <summary>Paso directo, mismo motivo que <see cref="WakeWordDetected"/>.</summary>
    public event EventHandler<WakeWordRecognitionObservedEventArgs>? RecognitionObserved
    {
        add => _wakeWordService.RecognitionObserved += value;
        remove => _wakeWordService.RecognitionObserved -= value;
    }

    public bool IsVoiceInputReady => _voiceInputService.IsReady;

    public bool IsVoiceInputListening => _voiceInputService.IsListening;

    public bool IsWakeWordReady => _wakeWordService.IsReady;

    public bool IsWakeWordListening => _wakeWordService.IsListening;

    /// <summary>Se aplica a los dos servicios: entrada de voz y wake word comparten micrófono.</summary>
    public int InputDeviceNumber
    {
        get => _voiceInputService.InputDeviceNumber;
        set
        {
            _voiceInputService.InputDeviceNumber = value;
            _wakeWordService.InputDeviceNumber = value;
        }
    }

    public WakeWordSensitivity WakeWordSensitivity
    {
        get => _wakeWordService.Sensitivity;
        set => _wakeWordService.Sensitivity = value;
    }

    public IReadOnlyList<string> WakeWordCustomAliases
    {
        get => _wakeWordService.CustomAliases;
        set => _wakeWordService.CustomAliases = value;
    }

    public IReadOnlyList<VoiceInputDevice> GetInputDevices() =>
        _voiceInputService.GetInputDevices();

    public Task<VoicePreparationResult> PrepareVoiceInputAsync(
        IProgress<VoicePreparationProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        _voiceInputService.PrepareAsync(progress, cancellationToken);

    public Task<VoicePreparationResult> PrepareWakeWordAsync(
        IProgress<VoicePreparationProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        _wakeWordService.PrepareAsync(progress, cancellationToken);

    /// <summary>
    /// Abre una sesión de push-to-talk: pausa wake word, detiene cualquier TTS en curso
    /// e inicia la escucha. Adquiere el candado de voz antes que el de wake word, nunca
    /// al revés. <see cref="IVoiceInputService.StartListeningAsync"/> ya se autoprepara
    /// si el motor no está listo, así que este método no lo duplica.
    ///
    /// Corrección 1.3A.1: si el arranque tiene éxito, <c>_voiceGate</c> se queda
    /// retenido representando la sesión abierta — este método **no** lo libera en su
    /// <c>finally</c>. Solo <see cref="StopPushToTalkAsync"/> o
    /// <see cref="CancelPushToTalkAsync"/> lo liberan, una vez exacta entre los dos. Si
    /// el arranque falla, se cancela o lanza antes de establecer la sesión, este método
    /// sí libera el candado él mismo, porque nunca llegó a existir una sesión que otro
    /// método pudiera cerrar (criterio 5 de la corrección).
    /// </summary>
    public async Task<VoiceStartResult> StartPushToTalkAsync(
        CancellationToken cancellationToken = default)
    {
        await _voiceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopWakeWordCoreAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            _voiceOutputService.Stop();
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _voiceInputService
                .StartListeningAsync(cancellationToken)
                .ConfigureAwait(false);

            if (result.IsAvailable)
            {
                // Sesión establecida: el candado queda retenido a propósito. No hay
                // `finally` que lo libere en esta rama.
                Interlocked.Exchange(ref _activeSession, new PushToTalkSession());
                return result;
            }

            // El servicio rechazó el arranque (p. ej. micrófono no disponible): nunca
            // hubo sesión, así que este método sí libera lo que adquirió.
            _voiceGate.Release();
            return result;
        }
        catch
        {
            _voiceGate.Release();
            throw;
        }
    }

    /// <summary>
    /// Cierra la sesión de push-to-talk activa, si la hay. Si <see cref="StopPushToTalkAsync"/>
    /// y <see cref="CancelPushToTalkAsync"/> se llaman de forma concurrente,
    /// <see cref="Interlocked.Exchange{T}(ref T, T)"/> decide un único ganador: quien
    /// obtiene la sesión (no nula) es el único que llama al servicio subyacente y
    /// libera <c>_voiceGate</c>, exactamente una vez, incluso si el servicio lanza. El
    /// perdedor (sesión ya nula) no hace nada — ni llama al servicio ni toca el
    /// candado — porque ya no hay nada que cerrar.
    /// </summary>
    public async Task<VoiceRecognitionResult> StopPushToTalkAsync(
        CancellationToken cancellationToken = default)
    {
        var session = Interlocked.Exchange(ref _activeSession, null);
        if (session is null)
        {
            return VoiceRecognitionResult.NoSpeech(
                "No había una sesión de push-to-talk activa que detener.");
        }

        try
        {
            return await _voiceInputService
                .StopListeningAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _voiceGate.Release();
        }
    }

    /// <summary>Misma disciplina de "único ganador" que <see cref="StopPushToTalkAsync"/>.</summary>
    public async Task CancelPushToTalkAsync()
    {
        var session = Interlocked.Exchange(ref _activeSession, null);
        if (session is null)
        {
            return;
        }

        try
        {
            await _voiceInputService.CancelAsync().ConfigureAwait(false);
        }
        finally
        {
            _voiceGate.Release();
        }
    }

    /// <summary>
    /// Escucha una orden completa tras detectar la frase de activación. Misma
    /// disciplina de candados que <see cref="StartPushToTalkAsync"/>: voz antes que
    /// wake word.
    /// </summary>
    public async Task<VoiceRecognitionResult> ListenAfterWakeWordAsync(
        TimeSpan maximumDuration,
        TimeSpan trailingSilence,
        ReadOnlyMemory<byte> initialPcmAudio = default,
        ReadOnlyMemory<byte> initialSpeechPcmAudio = default,
        CancellationToken cancellationToken = default)
    {
        await _voiceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopWakeWordCoreAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            _voiceOutputService.Stop();
            cancellationToken.ThrowIfCancellationRequested();
            return await _voiceInputService.ListenForUtteranceAsync(
                    maximumDuration,
                    trailingSilence,
                    initialPcmAudio,
                    initialSpeechPcmAudio,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _voiceGate.Release();
        }
    }

    public async Task<VoiceStartResult> StartWakeWordAsync(
        WakeWordPhrase phrase,
        CancellationToken cancellationToken = default)
    {
        await _wakeWordGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _wakeWordService
                .StartListeningAsync(phrase, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _wakeWordGate.Release();
        }
    }

    /// <summary>Detención completa. Nunca se cancela a medias: siempre corre hasta el final.</summary>
    public async Task StopWakeWordAsync()
    {
        await _wakeWordGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await _wakeWordService.StopListeningAsync().ConfigureAwait(false);
        }
        finally
        {
            _wakeWordGate.Release();
        }
    }

    /// <summary>
    /// Alias semántico de <see cref="StopWakeWordAsync"/> para las llamadas que hoy
    /// pausan (en vez de detener definitivamente) antes de otra operación de voz.
    /// Mismo mecanismo: no hay una operación distinta de "pausa" en los servicios
    /// subyacentes.
    /// </summary>
    public Task PauseWakeWordAsync() => StopWakeWordAsync();

    public void Speak(string text) => _voiceOutputService.SpeakShort(text);

    public void StopSpeaking() => _voiceOutputService.Stop();

    private async Task StopWakeWordCoreAsync()
    {
        await _wakeWordGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await _wakeWordService.StopListeningAsync().ConfigureAwait(false);
        }
        finally
        {
            _wakeWordGate.Release();
        }
    }

    /// <summary>
    /// Libera únicamente los dos <see cref="SemaphoreSlim"/> que este coordinador crea.
    /// No llama <c>Dispose()</c> sobre <see cref="IVoiceInputService"/>,
    /// <see cref="IVoiceOutputService"/> ni <see cref="IWakeWordService"/>: en esta
    /// subfase, el coordinador no es su dueño.
    ///
    /// Idempotente y seguro frente a llamadas concurrentes a este mismo método
    /// (<see cref="Interlocked.CompareExchange{T}(ref T, T, T)"/> decide un único
    /// ejecutor real). Es seguro llamarlo con una sesión de push-to-talk abierta o una
    /// operación de wake word en curso, porque ninguna de esas rutas mantiene un
    /// candado retenido dentro de un <c>await</c> activo en ese instante — solo lo
    /// mantiene retenido *entre* llamadas (mientras nadie está esperando dentro de
    /// <c>WaitAsync()</c>).
    ///
    /// Límite conocido, documentado en vez de resuelto: si una llamada está
    /// literalmente bloqueada dentro de <c>_voiceGate.WaitAsync()</c> o
    /// <c>_wakeWordGate.WaitAsync()</c> en el instante exacto en que <see cref="Dispose"/>
    /// libera esos semáforos, el comportamiento de esa espera pendiente queda indefinido
    /// por el propio contrato de <see cref="SemaphoreSlim"/> — no es algo que este tipo
    /// pueda garantizar sin un mecanismo de conteo de operaciones en vuelo (o
    /// <c>IAsyncDisposable</c>, explícitamente fuera de alcance de esta corrección).
    /// Contrato del llamador: no llamar <see cref="Dispose"/> mientras se sepa que hay
    /// una llamada bloqueada esperando adquirir un candado. Hoy el único llamador real
    /// (<c>KohanaCompositionRoot.Dispose</c>) lo invoca al cerrar la aplicación, cuando
    /// nada más está usando el coordinador.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        _voiceGate.Dispose();
        _wakeWordGate.Dispose();
    }
}
