using System.Windows;
using Nexo.App.WindowsIntegration;
using Nexo.Core.WindowsIntegration;
using Nexo.Windows.Composition;
using Nexo.Windows.Settings;
using Nexo.Windows.Storage;
using Nexo.Windows.WindowsIntegration;

namespace Nexo.App;

public partial class App : System.Windows.Application
{
    private SingleInstanceCoordinator? _singleInstance;
    private ManagedOllamaSupervisor? _managedOllamaSupervisor;
    private KohanaCompositionRoot? _compositionRoot;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // La migración es conservadora: copia datos de Nexo a Kohana sin borrar
        // ni sobrescribir la carpeta anterior. Un fallo no impide abrir la app.
        try
        {
            LegacyDataMigrator.MigrateIfNeeded();
        }
        catch (Exception)
        {
            // La configuración puede recrearse con valores seguros.
        }
        _singleInstance = new SingleInstanceCoordinator();
        if (!_singleInstance.IsPrimaryInstance)
        {
            _singleInstance.SignalPrimaryInstance();
            Shutdown();
            return;
        }

        var settingsStore = new JsonSettingsStore();
        var preferences = settingsStore.Load();
        var requestedHiddenStart = StartupCommandBuilder.ShouldStartHidden(e.Args);

        if (!preferences.HasCompletedOnboarding)
        {
            var onboarding = new OnboardingWindow(preferences, settingsStore);
            onboarding.ShowDialog();
            requestedHiddenStart = false;
        }

        _managedOllamaSupervisor = new ManagedOllamaSupervisor();
        _compositionRoot = new KohanaCompositionRoot();

        var mainWindow = new MainWindow(
            requestedHiddenStart,
            _managedOllamaSupervisor,
            _compositionRoot.AiChatService,
            _compositionRoot.AudioMixerService,
            _compositionRoot.ScreenCaptureService,
            _compositionRoot.VoiceCoordinator);
        MainWindow = mainWindow;

        _singleInstance.ActivationRequested += (_, _) =>
            Dispatcher.BeginInvoke(new Action(mainWindow.ShowFromBackground));
        _singleInstance.StartListening();

        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _managedOllamaSupervisor?.Dispose();
        _managedOllamaSupervisor = null;

        // Fase 1.3B3: KohanaCompositionRoot es dueño del subsistema de voz (el coordinador
        // más Whisper, TTS y Vosk) y los libera aquí, después de que Window_Closed ya haya
        // cancelado el token de vida y desuscrito los eventos de wake word. El
        // ServiceProvider no libera instancias que no creó él mismo (verificado), así que
        // esta es la única ruta de Dispose de esos servicios. Los demás servicios de
        // interfaz (IA, mezclador, captura) mantienen su liberación anterior.
        _compositionRoot?.Dispose();
        _compositionRoot = null;

        _singleInstance?.Dispose();
        _singleInstance = null;
        base.OnExit(e);
    }
}
