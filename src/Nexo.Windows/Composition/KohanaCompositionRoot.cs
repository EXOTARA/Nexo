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
/// Construye y resuelve, en un único <see cref="ServiceProvider"/>, los seis servicios de
/// interfaz que hoy fija <c>MainWindow</c> mediante inicializadores de campo con <c>new</c>.
/// Vive en <c>Nexo.Windows</c> (no en <c>Nexo.App</c>) para poder probarse sin arrastrar
/// <c>UseWPF</c>, siguiendo el mismo criterio ya aplicado a <c>SingleInstanceCoordinator</c>
/// en la fase 1.1. El único punto de la aplicación que crea esta clase es
/// <c>App.OnStartup</c>: sigue existiendo un único composition root y un único
/// <see cref="ServiceProvider"/> para toda la vida del proceso.
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
    /// Mecánica reutilizable de voz (fase 1.3A), construida sobre las mismas tres
    /// instancias expuestas arriba — no un cuarto motor. <c>MainWindow</c> todavía no la
    /// consume: sigue recibiendo <see cref="VoiceInputService"/>, <see cref="VoiceOutputService"/>
    /// y <see cref="WakeWordService"/> directamente, y sigue siendo su único dueño de
    /// ciclo de vida. Este coordinador no libera esas tres instancias en su propio
    /// <see cref="Voice.VoiceCoordinator.Dispose"/> — ver la nota en esa clase.
    /// </summary>
    public VoiceCoordinator VoiceCoordinator { get; }

    public KohanaCompositionRoot()
    {
        // Mismo tipo concreto y mismo orden relativo que los inicializadores de campo que
        // sustituye en MainWindow.xaml.cs (IAiChatService -> IAudioMixerService ->
        // IVoiceInputService -> IVoiceOutputService -> IWakeWordService -> IScreenCaptureService).
        var aiChatService = new AiChatRouterService();
        var audioMixerService = new WindowsAudioMixerService();
        var voiceInputService = new WhisperVoiceInputService();
        var voiceOutputService = new WindowsTextToSpeechService();
        var wakeWordService = new VoskWakeWordService();
        var screenCaptureService = new WindowsScreenCaptureService();
        var voiceCoordinator = new VoiceCoordinator(voiceInputService, voiceOutputService, wakeWordService);

        var services = new ServiceCollection();

        // Se registran las instancias ya construidas, no los tipos: el ServiceProvider no
        // libera instancias que no creó él mismo, así que Window_Closed sigue siendo la única
        // ruta que llama a Dispose() sobre estos seis servicios, exactamente como hoy.
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

        // Libera solo los recursos propios del coordinador (dos SemaphoreSlim); no toca
        // los tres servicios de voz, que Window_Closed sigue liberando en MainWindow.
        VoiceCoordinator.Dispose();
        Provider.Dispose();
    }
}
