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

    // ---------- Fase 1.3A: VoiceCoordinator ----------

    [Fact]
    public void VoiceCoordinator_IsASingleInstance()
    {
        using var root = new KohanaCompositionRoot();

        Assert.Same(root.VoiceCoordinator, root.Provider.GetRequiredService<VoiceCoordinator>());
        Assert.Same(
            root.Provider.GetRequiredService<VoiceCoordinator>(),
            root.Provider.GetRequiredService<VoiceCoordinator>());
    }

    [Fact]
    public void VoiceCoordinator_WrapsTheExactSameWakeWordAndVoiceInputInstances()
    {
        // No hay forma de exponer los tres servicios internos del coordinador sin romper
        // su superficie mínima (a propósito, ver VoiceCoordinator.cs). En su lugar, se
        // verifica identidad por comportamiento: si el coordinador y `root` compartieran
        // instancias distintas, escribir por uno no se reflejaría al leer por el otro.
        using var root = new KohanaCompositionRoot();

        root.VoiceCoordinator.WakeWordSensitivity = WakeWordSensitivity.High;
        Assert.Equal(WakeWordSensitivity.High, root.WakeWordService.Sensitivity);

        // VoskWakeWordService.CustomAliases devuelve una copia defensiva en cada lectura,
        // así que la identidad se verifica por contenido, no por referencia.
        IReadOnlyList<string> aliases = ["identidad de prueba"];
        root.VoiceCoordinator.WakeWordCustomAliases = aliases;
        Assert.Equal(aliases, root.WakeWordService.CustomAliases);

        root.VoiceCoordinator.InputDeviceNumber = 5;
        Assert.Equal(5, root.VoiceInputService.InputDeviceNumber);
        Assert.Equal(5, root.WakeWordService.InputDeviceNumber);
    }

    [Fact]
    public void NoFourthSetOfVoiceEnginesIsRegistered()
    {
        using var root = new KohanaCompositionRoot();

        Assert.Single(root.Provider.GetServices<IVoiceInputService>());
        Assert.Single(root.Provider.GetServices<IVoiceOutputService>());
        Assert.Single(root.Provider.GetServices<IWakeWordService>());
        Assert.Single(root.Provider.GetServices<VoiceCoordinator>());
    }

    [Fact]
    public void Dispose_DisposesTheCoordinatorsOwnResourcesButNotTheThreeVoiceServices()
    {
        var root = new KohanaCompositionRoot();
        var voiceInputService = root.VoiceInputService;
        var voiceOutputService = root.VoiceOutputService;
        var wakeWordService = root.WakeWordService;

        root.Dispose();

        // El coordinador ya liberó sus propios candados; los tres servicios de voz
        // siguen intactos porque MainWindow (aún) es su único dueño de ciclo de vida.
        var exception = Record.Exception(() =>
        {
            _ = voiceInputService.IsReady;
            voiceOutputService.Stop();
            wakeWordService.Sensitivity = wakeWordService.Sensitivity;
        });
        Assert.Null(exception);
    }
}
