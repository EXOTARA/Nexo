using Microsoft.Extensions.DependencyInjection;
using Nexo.Core.Ai;
using Nexo.Core.Audio;
using Nexo.Core.Vision;
using Nexo.Core.Voice;
using Nexo.Windows.Ai;
using Nexo.Windows.Audio;
using Nexo.Windows.Composition;
using Nexo.Windows.Vision;
using Nexo.Windows.Voice;

namespace Nexo.Windows.Tests.Composition;

public sealed class KohanaCompositionRootTests
{
    [Fact]
    public void Constructor_ResolvesAllSixServicesWithExpectedConcreteTypes()
    {
        using var root = new KohanaCompositionRoot();

        Assert.IsType<AiChatRouterService>(root.AiChatService);
        Assert.IsType<WindowsAudioMixerService>(root.AudioMixerService);
        Assert.IsType<WhisperVoiceInputService>(root.VoiceInputService);
        Assert.IsType<WindowsTextToSpeechService>(root.VoiceOutputService);
        Assert.IsType<VoskWakeWordService>(root.WakeWordService);
        Assert.IsType<WindowsScreenCaptureService>(root.ScreenCaptureService);
    }

    [Fact]
    public void Constructor_ResolvedInstancesMatchWhatTheContainerResolves()
    {
        using var root = new KohanaCompositionRoot();

        // El provider debe devolver exactamente la misma instancia que las propiedades
        // expuestas: la resolución ansiosa no debe divergir del propio contenedor.
        Assert.Same(root.AiChatService, root.Provider.GetRequiredService<IAiChatService>());
        Assert.Same(root.AudioMixerService, root.Provider.GetRequiredService<IAudioMixerService>());
        Assert.Same(root.VoiceInputService, root.Provider.GetRequiredService<IVoiceInputService>());
        Assert.Same(root.VoiceOutputService, root.Provider.GetRequiredService<IVoiceOutputService>());
        Assert.Same(root.WakeWordService, root.Provider.GetRequiredService<IWakeWordService>());
        Assert.Same(root.ScreenCaptureService, root.Provider.GetRequiredService<IScreenCaptureService>());
    }

    [Fact]
    public void Provider_ResolvingTwiceReturnsSameSingletonInstance()
    {
        using var root = new KohanaCompositionRoot();

        Assert.Same(
            root.Provider.GetRequiredService<IWakeWordService>(),
            root.Provider.GetRequiredService<IWakeWordService>());
        Assert.Same(
            root.Provider.GetRequiredService<IAiChatService>(),
            root.Provider.GetRequiredService<IAiChatService>());
    }

    [Fact]
    public void Constructor_ProducesSixDistinctServiceInstances()
    {
        using var root = new KohanaCompositionRoot();

        var instances = new object[]
        {
            root.AiChatService,
            root.AudioMixerService,
            root.VoiceInputService,
            root.VoiceOutputService,
            root.WakeWordService,
            root.ScreenCaptureService
        };

        Assert.Equal(instances.Length, instances.Distinct().Count());
    }

    [Fact]
    public void Dispose_DoesNotThrowAndIsIdempotent()
    {
        var root = new KohanaCompositionRoot();

        var exception = Record.Exception(() =>
        {
            root.Dispose();
            root.Dispose();
        });

        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_DoesNotDisposeTheUnderlyingServiceInstances()
    {
        // El contenedor registra instancias ya construidas, no tipos: no debe liberar
        // IWakeWordService/IVoiceInputService/IVoiceOutputService al liberarse a sí mismo,
        // porque Window_Closed en MainWindow sigue siendo la única ruta responsable de eso.
        var root = new KohanaCompositionRoot();
        var wakeWordService = root.WakeWordService;

        root.Dispose();

        var exception = Record.Exception(() => wakeWordService.Sensitivity = wakeWordService.Sensitivity);
        Assert.Null(exception);
    }
}
