using Microsoft.Extensions.DependencyInjection;
using Nexo.Core.Ai;
using Nexo.Core.Audio;
using Nexo.Core.Vision;
using Nexo.Core.Voice;
using Nexo.Windows.Ai;
using Nexo.Windows.Audio;
using Nexo.Windows.Vision;
using Nexo.Windows.Voice;

namespace Nexo.Windows.Composition;

/// <summary>
/// Raíz de composición única de Kohana: construye, en un único <see cref="ServiceProvider"/>,
/// los seis servicios de interfaz de la aplicación y el <see cref="Voice.VoiceCoordinator"/>, y
/// es su propietario de ciclo de vida. La crea exclusivamente <c>App.OnStartup</c>, de modo que
/// existe una sola composición y un solo <see cref="ServiceProvider"/> durante toda la vida del
/// proceso.
///
/// Vive en <c>Nexo.Windows</c> (no en <c>Nexo.App</c>) para poder probarse sin arrastrar
/// <c>UseWPF</c>: expone el grafo compuesto (contenedor y servicios resueltos) para que las
/// pruebas verifiquen la composición. La capa de aplicación, en cambio, consume solo los
/// servicios de IA, audio y captura y el coordinador de voz; a los tres motores de voz
/// (Whisper, TTS, Vosk) accede únicamente a través de <see cref="VoiceCoordinator"/>. La
/// liberación de todo el subsistema de voz ocurre aquí (ver <see cref="Dispose"/>).
/// </summary>
public sealed class KohanaCompositionRoot : IDisposable
{
    private bool _disposed;

    public ServiceProvider Provider { get; }

    public IAiChatService AiChatService { get; }
    public IAudioMixerService AudioMixerService { get; }
    public IVoiceInputService VoiceInputService { get; }
    public IVoiceOutputService VoiceOutputService { get; }
    public IWakeWordService WakeWordService { get; }
    public IScreenCaptureService ScreenCaptureService { get; }

    /// <summary>
    /// Único punto de acceso al subsistema de voz para la capa de aplicación: <c>MainWindow</c>
    /// no recibe los tres motores directamente. Construido sobre las mismas instancias que este
    /// composition root posee — no un cuarto motor. El coordinador es dueño de su
    /// sincronización (dos <see cref="System.Threading.SemaphoreSlim"/>) pero **no** del ciclo
    /// de vida de Whisper, TTS y Vosk: eso es responsabilidad de este composition root
    /// (ver <see cref="Dispose"/>).
    /// </summary>
    public VoiceCoordinator VoiceCoordinator { get; }

    public KohanaCompositionRoot()
    {
        // Orden de construcción de los seis servicios de interfaz
        // (IAiChatService -> IAudioMixerService -> IVoiceInputService -> IVoiceOutputService ->
        // IWakeWordService -> IScreenCaptureService).
        var aiChatService = new AiChatRouterService();
        var audioMixerService = new WindowsAudioMixerService();
        var voiceInputService = new WhisperVoiceInputService();
        var voiceOutputService = new WindowsTextToSpeechService();
        var wakeWordService = new VoskWakeWordService();
        var screenCaptureService = new WindowsScreenCaptureService();
        var voiceCoordinator = new VoiceCoordinator(voiceInputService, voiceOutputService, wakeWordService);

        var services = new ServiceCollection();

        // Se registran las instancias ya construidas, no los tipos: el ServiceProvider no
        // libera instancias que no creó él mismo (verificado empíricamente), así que la
        // liberación de estos servicios es responsabilidad explícita de este composition root
        // (subsistema de voz) o de MainWindow.Window_Closed (IA), nunca del contenedor.
        services.AddSingleton<IAiChatService>(aiChatService);
        services.AddSingleton<IAudioMixerService>(audioMixerService);
        services.AddSingleton<IVoiceInputService>(voiceInputService);
        services.AddSingleton<IVoiceOutputService>(voiceOutputService);
        services.AddSingleton<IWakeWordService>(wakeWordService);
        services.AddSingleton<IScreenCaptureService>(screenCaptureService);
        services.AddSingleton(voiceCoordinator);

        Provider = services.BuildServiceProvider();

        // Resolución ansiosa desde el contenedor, en el mismo orden, antes de que MainWindow
        // cablee ningún evento.
        AiChatService = Provider.GetRequiredService<IAiChatService>();
        AudioMixerService = Provider.GetRequiredService<IAudioMixerService>();
        VoiceInputService = Provider.GetRequiredService<IVoiceInputService>();
        VoiceOutputService = Provider.GetRequiredService<IVoiceOutputService>();
        WakeWordService = Provider.GetRequiredService<IWakeWordService>();
        ScreenCaptureService = Provider.GetRequiredService<IScreenCaptureService>();
        VoiceCoordinator = Provider.GetRequiredService<VoiceCoordinator>();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Este composition root es el dueño del subsistema de voz y su única ruta de Dispose.
        // Libera primero el coordinador (sus dos SemaphoreSlim) y después los tres motores en
        // el mismo orden relativo histórico (wake word -> salida de voz -> entrada de voz). Cada
        // Dispose es idempotente. El ServiceProvider no libera instancias registradas.
        VoiceCoordinator.Dispose();
        WakeWordService.Dispose();
        VoiceOutputService.Dispose();
        VoiceInputService.Dispose();
        Provider.Dispose();
    }
}
