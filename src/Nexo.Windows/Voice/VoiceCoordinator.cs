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
    private readonly IVoiceInputService _voiceInputService;
    private readonly IVoiceOutputService _voiceOutputService;
    private readonly IWakeWordService _wakeWordService;

    // Recursos propios del coordinador (no los tres servicios): se crean y se liberan aquí.
    private readonly SemaphoreSlim _voiceGate = new(1, 1);
    private readonly SemaphoreSlim _wakeWordGate = new(1, 1);

    private bool _disposed;

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
    /// Pausa wake word, detiene cualquier TTS en curso e inicia la escucha de
    /// push-to-talk. Adquiere el candado de voz antes que el de wake word, nunca al
    /// revés. <see cref="IVoiceInputService.StartListeningAsync"/> ya se autoprepara si
    /// el motor no está listo, así que este método no lo duplica.
    /// </summary>
    public async Task<VoiceStartResult> StartPushToTalkAsync(
        CancellationToken cancellationToken = default)
    {
        await _voiceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopWakeWordWithinCoordinatorGateAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            _voiceOutputService.Stop();
            cancellationToken.ThrowIfCancellationRequested();
            return await _voiceInputService
                .StartListeningAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _voiceGate.Release();
        }
    }

    public async Task<VoiceRecognitionResult> StopPushToTalkAsync(
        CancellationToken cancellationToken = default)
    {
        await _voiceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
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

    public async Task CancelPushToTalkAsync()
    {
        await _voiceGate.WaitAsync().ConfigureAwait(false);
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
            await StopWakeWordWithinCoordinatorGateAsync().ConfigureAwait(false);
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

    // ---------------------------------------------------------------------------------
    // Ámbitos de exclusión (diseño final de la fase 1.3B3).
    //
    // El único mecanismo de sincronización real del subsistema de voz vive aquí: los dos
    // SemaphoreSlim privados de arriba. Un orquestador externo (la vista principal) ya no
    // tiene semáforos propios: adquiere un ámbito, hace su sección crítica dentro de él —
    // incluida toda su orquestación visual— y lo libera al terminar. Las operaciones
    // mutantes de cada servicio solo son alcanzables a través del ámbito correspondiente,
    // así que es imposible mutar un servicio sin sostener su exclusión.
    //
    // El orden de adquisición es siempre entrada de voz primero y wake word después
    // (nunca al revés): cuando un ámbito de voz envuelve a uno de wake word —lo habitual
    // en push-to-talk y en la escucha tras la palabra de activación— la anidación respeta
    // ese orden porque son dos semáforos distintos.
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Adquiere el semáforo de entrada de voz y devuelve un ámbito que lo libera al
    /// desecharse. Si el token se cancela antes de adquirirlo, no se toma el candado y no
    /// hay nada que liberar.
    /// </summary>
    public async Task<IVoiceInputScope> AcquireVoiceInputScopeAsync(
        CancellationToken cancellationToken = default)
    {
        await _voiceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new VoiceInputScope(this);
    }

    /// <summary>
    /// Adquiere el semáforo de wake word y devuelve un ámbito que lo libera al desecharse.
    /// Mismo contrato que <see cref="AcquireVoiceInputScopeAsync"/>.
    /// </summary>
    public async Task<IWakeWordScope> AcquireWakeWordScopeAsync(
        CancellationToken cancellationToken = default)
    {
        await _wakeWordGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new WakeWordScope(this);
    }

    /// <summary>
    /// Ámbito de entrada de voz: delega cada operación en el servicio subyacente y libera
    /// <see cref="_voiceGate"/> una sola vez, aunque se deseche más de una vez.
    /// </summary>
    private sealed class VoiceInputScope : IVoiceInputScope
    {
        private readonly VoiceCoordinator _owner;
        private bool _released;

        public VoiceInputScope(VoiceCoordinator owner) => _owner = owner;

        public Task<VoiceStartResult> StartListeningAsync(CancellationToken cancellationToken = default) =>
            _owner._voiceInputService.StartListeningAsync(cancellationToken);

        public Task<VoiceRecognitionResult> StopListeningAsync(CancellationToken cancellationToken = default) =>
            _owner._voiceInputService.StopListeningAsync(cancellationToken);

        public Task CancelAsync() => _owner._voiceInputService.CancelAsync();

        public Task<VoiceRecognitionResult> ListenForUtteranceAsync(
            TimeSpan maximumDuration,
            TimeSpan trailingSilence,
            ReadOnlyMemory<byte> initialPcmAudio = default,
            ReadOnlyMemory<byte> initialSpeechPcmAudio = default,
            CancellationToken cancellationToken = default) =>
            _owner._voiceInputService.ListenForUtteranceAsync(
                maximumDuration,
                trailingSilence,
                initialPcmAudio,
                initialSpeechPcmAudio,
                cancellationToken);

        public ValueTask DisposeAsync()
        {
            if (!_released)
            {
                _released = true;
                _owner._voiceGate.Release();
            }

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Ámbito de wake word: delega cada operación en el servicio subyacente y libera
    /// <see cref="_wakeWordGate"/> una sola vez, aunque se deseche más de una vez.
    /// </summary>
    private sealed class WakeWordScope : IWakeWordScope
    {
        private readonly VoiceCoordinator _owner;
        private bool _released;

        public WakeWordScope(VoiceCoordinator owner) => _owner = owner;

        public Task<VoiceStartResult> StartListeningAsync(
            WakeWordPhrase phrase,
            CancellationToken cancellationToken = default) =>
            _owner._wakeWordService.StartListeningAsync(phrase, cancellationToken);

        public Task StopListeningAsync() => _owner._wakeWordService.StopListeningAsync();

        public ValueTask DisposeAsync()
        {
            if (!_released)
            {
                _released = true;
                _owner._wakeWordGate.Release();
            }

            return ValueTask.CompletedTask;
        }
    }

    // ---------------------------------------------------------------------------------
    // Operaciones bajo coordinación externa (fase 1.3B2A) — API de transición.
    //
    // Delegaciones transparentes a los servicios subyacentes que **no adquieren ninguno
    // de los dos candados internos de este coordinador**. Existen para que un
    // orquestador que ya posee la exclusión pueda dejar de llamar a los servicios de
    // forma directa sin que cambie todavía el propietario de la sincronización.
    //
    // Durante la fase 1.3B2 ese orquestador es la vista principal de la aplicación, que
    // conserva sus propios semáforos. Esta API **no es definitiva**: cuando la propiedad
    // de los candados se transfiera a este coordinador, estas operaciones dejarán de
    // tener razón de ser y se retirarán junto con esa transferencia.
    // ---------------------------------------------------------------------------------

    /// <summary>
    /// Inicia la escucha de entrada de voz delegando directamente en el servicio.
    /// </summary>
    /// <remarks>
    /// No adquiere <c>_voiceGate</c> ni <c>_wakeWordGate</c>. Solo debe llamarlo un
    /// orquestador que ya garantice la exclusión necesaria sobre el micrófono; en la
    /// transición actual, la vista principal de la aplicación. **No combinar dentro de
    /// la misma sección crítica con los métodos compuestos** (<see cref="StartPushToTalkAsync"/>
    /// y equivalentes), que sí adquieren los candados internos: hacerlo crearía dos
    /// dominios de exclusión sobre el mismo servicio. API de transición, no definitiva.
    /// </remarks>
    public Task<VoiceStartResult> StartVoiceInputUnderExternalCoordinationAsync(
        CancellationToken cancellationToken = default) =>
        _voiceInputService.StartListeningAsync(cancellationToken);

    /// <summary>
    /// Detiene la escucha de entrada de voz delegando directamente en el servicio.
    /// </summary>
    /// <remarks>
    /// No adquiere <c>_voiceGate</c> ni <c>_wakeWordGate</c>. Solo debe llamarlo un
    /// orquestador que ya garantice la exclusión necesaria; en la transición actual, la
    /// vista principal de la aplicación. **No combinar dentro de la misma sección
    /// crítica con los métodos compuestos** que adquieren los candados internos. API de
    /// transición, no definitiva.
    /// </remarks>
    public Task<VoiceRecognitionResult> StopVoiceInputUnderExternalCoordinationAsync(
        CancellationToken cancellationToken = default) =>
        _voiceInputService.StopListeningAsync(cancellationToken);

    /// <summary>
    /// Cancela la escucha de entrada de voz delegando directamente en el servicio.
    /// </summary>
    /// <remarks>
    /// No adquiere <c>_voiceGate</c> ni <c>_wakeWordGate</c>, a diferencia de
    /// <see cref="CancelPushToTalkAsync"/>. Solo debe llamarlo un orquestador que ya
    /// garantice la exclusión necesaria; en la transición actual, la vista principal de
    /// la aplicación. **No combinar dentro de la misma sección crítica con los métodos
    /// compuestos** que adquieren los candados internos. API de transición, no definitiva.
    /// </remarks>
    public Task CancelVoiceInputUnderExternalCoordinationAsync() =>
        _voiceInputService.CancelAsync();

    /// <summary>
    /// Escucha una orden completa delegando directamente en el servicio, preservando el
    /// orden exacto de argumentos de <see cref="IVoiceInputService.ListenForUtteranceAsync"/>.
    /// </summary>
    /// <remarks>
    /// No adquiere <c>_voiceGate</c> ni <c>_wakeWordGate</c>, y **no pausa wake word ni
    /// detiene el TTS**, a diferencia de <see cref="ListenAfterWakeWordAsync"/>: esos
    /// pasos siguen siendo responsabilidad del orquestador. Solo debe llamarlo un
    /// orquestador que ya garantice la exclusión necesaria; en la transición actual, la
    /// vista principal de la aplicación. **No combinar dentro de la misma sección
    /// crítica con los métodos compuestos** que adquieren los candados internos. API de
    /// transición, no definitiva.
    /// </remarks>
    public Task<VoiceRecognitionResult> ListenForUtteranceUnderExternalCoordinationAsync(
        TimeSpan maximumDuration,
        TimeSpan trailingSilence,
        ReadOnlyMemory<byte> initialPcmAudio = default,
        ReadOnlyMemory<byte> initialSpeechPcmAudio = default,
        CancellationToken cancellationToken = default) =>
        _voiceInputService.ListenForUtteranceAsync(
            maximumDuration,
            trailingSilence,
            initialPcmAudio,
            initialSpeechPcmAudio,
            cancellationToken);

    /// <summary>
    /// Inicia la escucha de wake word delegando directamente en el servicio.
    /// </summary>
    /// <remarks>
    /// No adquiere <c>_wakeWordGate</c> ni <c>_voiceGate</c>, a diferencia de
    /// <see cref="StartWakeWordAsync"/>. Solo debe llamarlo un orquestador que ya
    /// garantice la exclusión necesaria; en la transición actual, la vista principal de
    /// la aplicación. **No combinar dentro de la misma sección crítica con los métodos
    /// compuestos** que adquieren los candados internos. API de transición, no definitiva.
    /// </remarks>
    public Task<VoiceStartResult> StartWakeWordUnderExternalCoordinationAsync(
        WakeWordPhrase phrase,
        CancellationToken cancellationToken = default) =>
        _wakeWordService.StartListeningAsync(phrase, cancellationToken);

    /// <summary>
    /// Detiene la escucha de wake word delegando directamente en el servicio.
    /// </summary>
    /// <remarks>
    /// No adquiere <c>_wakeWordGate</c> ni <c>_voiceGate</c>, a diferencia de
    /// <see cref="StopWakeWordAsync"/> y del helper privado
    /// <c>StopWakeWordWithinCoordinatorGateAsync</c>. Solo debe llamarlo un orquestador
    /// que ya garantice la exclusión necesaria; en la transición actual, la vista
    /// principal de la aplicación. **No combinar dentro de la misma sección crítica con
    /// los métodos compuestos** que adquieren los candados internos. API de transición,
    /// no definitiva.
    /// </remarks>
    public Task StopWakeWordUnderExternalCoordinationAsync() =>
        _wakeWordService.StopListeningAsync();

    /// <summary>
    /// Detención de wake word ejecutada **dentro** del dominio de candados de este
    /// coordinador: adquiere y libera <c>_wakeWordGate</c>. El nombre lo distingue
    /// explícitamente de las operaciones <c>…UnderExternalCoordinationAsync</c>, que no
    /// adquieren ningún candado porque el llamador ya aporta la exclusión.
    /// </summary>
    private async Task StopWakeWordWithinCoordinatorGateAsync()
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
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _voiceGate.Dispose();
        _wakeWordGate.Dispose();
    }
}
