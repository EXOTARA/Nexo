using System.Windows;
using Nexo.App.WindowsIntegration;
using Nexo.Core.WindowsIntegration;
using Nexo.Windows.Settings;
using Nexo.Windows.Storage;

namespace Nexo.App;

public partial class App : System.Windows.Application
{
    private SingleInstanceCoordinator? _singleInstance;
    private ManagedOllamaSupervisor? _managedOllamaSupervisor;

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

        var mainWindow = new MainWindow(
            requestedHiddenStart,
            _managedOllamaSupervisor);
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

        _singleInstance?.Dispose();
        _singleInstance = null;
        base.OnExit(e);
    }
}
