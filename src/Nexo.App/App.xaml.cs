using System.Windows;
using Nexo.App.WindowsIntegration;
using Nexo.Core.WindowsIntegration;
using Nexo.Windows.Settings;

namespace Nexo.App;

public partial class App : System.Windows.Application
{
    private SingleInstanceCoordinator? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
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

        var mainWindow = new MainWindow(requestedHiddenStart);
        MainWindow = mainWindow;

        _singleInstance.ActivationRequested += (_, _) =>
            Dispatcher.BeginInvoke(new Action(mainWindow.ShowFromBackground));
        _singleInstance.StartListening();

        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        _singleInstance = null;
        base.OnExit(e);
    }
}
