using System.ComponentModel;
using System.Net.Http;
using System.Windows;
using Nexo.Core.Ai;
using Nexo.Core.Settings;
using Nexo.Core.Voice;
using Nexo.Windows.Ai;
using Nexo.Windows.Settings;
using Nexo.Windows.Voice;
using Nexo.Windows.WindowsIntegration;

namespace Nexo.App;

public partial class OnboardingWindow : Window
{
    private readonly ShellPreferences _preferences;
    private readonly JsonSettingsStore _settingsStore;
    private readonly WindowsStartupService _startupService = new();
    private readonly OllamaModelService _modelService = new();
    private readonly WhisperVoiceInputService _voiceInputService = new();
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly string[] _stepTitles =
    [
        "Bienvenido",
        "Configura la voz",
        "Conecta la IA local",
        "Privacidad y Windows"
    ];

    private int _step;
    private bool _allowClose;

    public OnboardingWindow(
        ShellPreferences preferences,
        JsonSettingsStore settingsStore)
    {
        InitializeComponent();
        _preferences = preferences;
        _settingsStore = settingsStore;
        _preferences.StartWithWindows = _startupService.IsEnabled();

        WakeWordCheckBox.IsChecked = preferences.WakeWordEnabled;
        VisionCheckBox.IsChecked = preferences.VisionEnabled;
        StartWithWindowsCheckBox.IsChecked = preferences.StartWithWindows;
        MinimizeToTrayCheckBox.IsChecked = preferences.MinimizeToTray;
        WindowsNotificationsCheckBox.IsChecked = preferences.ShowWindowsNotifications;
        NotificationSoundsCheckBox.IsChecked = preferences.PlayNotificationSounds;
        AiModelComboBox.Text = preferences.AiModel;
        ShowStep(0);
    }

    public bool WasCompleted { get; private set; }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        LoadMicrophones();
        await LoadModelsAsync();
    }

    private void LoadMicrophones()
    {
        var devices = _voiceInputService.GetInputDevices();
        MicrophoneComboBox.ItemsSource = devices;
        MicrophoneComboBox.SelectedValue = devices.Any(device =>
            device.DeviceNumber == _preferences.VoiceInputDeviceNumber)
            ? _preferences.VoiceInputDeviceNumber
            : devices.FirstOrDefault()?.DeviceNumber ?? -1;
        MicrophoneStatusText.Text = devices.Count switch
        {
            0 => "Windows no informó micrófonos disponibles. Puedes continuar y configurarlo después.",
            1 => "Se encontró un micrófono.",
            _ => $"Se encontraron {devices.Count} micrófonos."
        };
    }

    private async Task LoadModelsAsync()
    {
        AiStatusText.Text = "Comprobando Ollama…";
        try
        {
            var selectedModel = (AiModelComboBox.Text ?? string.Empty).Trim();
            var models = await _modelService.ListAsync(
                "http://localhost:11434/v1",
                _lifetimeCancellation.Token);
            var modelNames = models.Select(model => model.Name).ToArray();
            AiModelComboBox.ItemsSource = modelNames;
            var selectedIndex = Array.FindIndex(modelNames, model =>
                model.Equals(selectedModel, StringComparison.OrdinalIgnoreCase));
            if (selectedIndex >= 0)
            {
                AiModelComboBox.SelectedIndex = selectedIndex;
            }
            else if (!string.IsNullOrWhiteSpace(selectedModel))
            {
                AiModelComboBox.Text = selectedModel;
            }
            else if (models.Count > 0)
            {
                AiModelComboBox.SelectedIndex = 0;
            }

            AiStatusText.Text = models.Count == 0
                ? "Ollama respondió, pero no hay modelos instalados."
                : $"Ollama conectado · {models.Count} modelo(s) instalado(s).";
        }
        catch (HttpRequestException)
        {
            AiStatusText.Text = "No pude conectar con Ollama. Puedes omitir este paso y configurarlo después.";
        }
        catch (Exception exception)
        {
            AiStatusText.Text = $"No pude consultar Ollama: {exception.Message}";
        }
    }

    private void ShowStep(int step)
    {
        _step = Math.Clamp(step, 0, 3);
        WelcomePanel.Visibility = _step == 0 ? Visibility.Visible : Visibility.Collapsed;
        VoicePanel.Visibility = _step == 1 ? Visibility.Visible : Visibility.Collapsed;
        AiPanel.Visibility = _step == 2 ? Visibility.Visible : Visibility.Collapsed;
        PrivacyPanel.Visibility = _step == 3 ? Visibility.Visible : Visibility.Collapsed;
        StepTitleText.Text = _stepTitles[_step];
        StepIndicatorText.Text = $"{_step + 1} de 4";
        BackButton.IsEnabled = _step > 0;
        NextButton.Content = _step == 3 ? "Terminar" : "Siguiente";
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) =>
        ShowStep(_step - 1);

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (_step < 3)
        {
            ShowStep(_step + 1);
            return;
        }

        CompleteOnboarding();
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        _preferences.HasCompletedOnboarding = true;
        _settingsStore.Save(_preferences);
        WasCompleted = true;
        _allowClose = true;
        Close();
    }

    private async void RefreshModelsButton_Click(object sender, RoutedEventArgs e) =>
        await LoadModelsAsync();

    private async void ManageModelsButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new ModelManagerWindow(
            "http://localhost:11434/v1",
            AiModelComboBox.Text)
        {
            Owner = this
        };
        if (window.ShowDialog() == true && !string.IsNullOrWhiteSpace(window.SelectedModel))
        {
            AiModelComboBox.Text = window.SelectedModel;
        }

        await LoadModelsAsync();
    }

    private void CompleteOnboarding()
    {
        if (MicrophoneComboBox.SelectedItem is VoiceInputDevice microphone)
        {
            _preferences.VoiceInputDeviceNumber = microphone.DeviceNumber;
        }

        _preferences.WakeWordEnabled = WakeWordCheckBox.IsChecked == true;
        _preferences.WakeWordPhrase = WakeWordPhrase.Nexo;

        var rawModel = (AiModelComboBox.Text ?? string.Empty).Trim();
        var model = string.IsNullOrWhiteSpace(rawModel)
            ? null
            : OllamaModelName.Normalize(rawModel);
        if (!string.IsNullOrWhiteSpace(rawModel) && model is null)
        {
            AiStatusText.Text = "El nombre del modelo contiene caracteres no válidos.";
            ShowStep(2);
            return;
        }

        if (model is null)
        {
            _preferences.AiProvider = AiProviderKind.Disabled;
            _preferences.AiBaseUrl = string.Empty;
            _preferences.AiModel = string.Empty;
        }
        else
        {
            _preferences.AiProvider = AiProviderKind.Ollama;
            _preferences.AiBaseUrl = "http://localhost:11434/v1";
            _preferences.AiModel = model;
            _preferences.AiApiKeyEnvironmentVariable = string.Empty;
        }

        _preferences.VisionEnabled = VisionCheckBox.IsChecked == true;
        _preferences.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
        _preferences.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked == true;
        _preferences.ShowWindowsNotifications = WindowsNotificationsCheckBox.IsChecked == true;
        _preferences.PlayNotificationSounds = NotificationSoundsCheckBox.IsChecked == true;
        _preferences.HasCompletedOnboarding = true;

        var startupResult = _startupService.SetEnabled(_preferences.StartWithWindows);
        if (!startupResult.Success && _preferences.StartWithWindows)
        {
            _preferences.StartWithWindows = false;
            FinishStatusText.Text = startupResult.Message;
            MessageBox.Show(
                this,
                startupResult.Message,
                "Inicio con Windows",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        _settingsStore.Save(_preferences);
        WasCompleted = true;
        _allowClose = true;
        Close();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose)
        {
            var result = MessageBox.Show(
                this,
                "¿Omitir la configuración inicial? Puedes repetirla después desde Personalización.",
                "Configurar Nexo",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                e.Cancel = true;
                return;
            }

            _preferences.HasCompletedOnboarding = true;
            _settingsStore.Save(_preferences);
        }

        _lifetimeCancellation.Cancel();
        _voiceInputService.Dispose();
        _modelService.Dispose();
        _lifetimeCancellation.Dispose();
    }
}
