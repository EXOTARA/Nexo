using System.Windows;
using System.Windows.Controls;
using Nexo.Core.Ai;
using Nexo.Core.Settings;
using Nexo.Core.Voice;

namespace Nexo.App.Views;

public partial class SettingsView : UserControl
{
    private bool _isApplyingPreferences;

    public event Action<SidebarPosition>? PositionChanged;
    public event Action<double>? WidthChanged;
    public event Action<double>? OpacityChanged;
    public event Action<string>? AccentChanged;
    public event Action<bool>? AnimationsChanged;
    public event Action<string, bool>? ModuleVisibilityChanged;
    public event Action<string, bool>? PeekOptionChanged;
    public event Action<bool>? ConversationHistoryChanged;
    public event Action<bool>? VoiceResponsesChanged;
    public event Action<int>? VoiceInputDeviceChanged;
    public event Action<bool>? WakeWordEnabledChanged;
    public event Action<WakeWordPhrase>? WakeWordPhraseChanged;
    public event Action<WakeWordSensitivity>? WakeWordSensitivityChanged;
    public event EventHandler? WakeWordTestRequested;
    public event EventHandler? WakeWordAliasFromLastRequested;
    public event EventHandler? WakeWordAliasesClearRequested;
    public event Action<AiProviderKind>? AiProviderChanged;
    public event Action<string>? AiBaseUrlChanged;
    public event Action<string>? AiModelChanged;
    public event Action<string>? AiApiKeyEnvironmentVariableChanged;
    public event Action<bool>? ShareSystemMetricsWithAiChanged;
    public event Action<bool>? VisionEnabledChanged;
    public event Action<bool>? ResourceGovernorEnabledChanged;
    public event Action<bool>? PauseWakeWordInGameModeChanged;
    public event Action<bool>? ProtectVisionWhenBusyChanged;
    public event Action<bool>? StartWithWindowsChanged;
    public event Action<bool>? MinimizeToTrayChanged;
    public event Action<bool>? WindowsNotificationsChanged;
    public event Action<bool>? NotificationSoundsChanged;
    public event EventHandler? AiTestConnectionRequested;
    public event EventHandler? ManageModelsRequested;
    public event EventHandler? DiagnosticsRequested;
    public event EventHandler? OnboardingRequested;

    public SettingsView()
    {
        InitializeComponent();
    }

    public void ApplyPreferences(ShellPreferences preferences)
    {
        _isApplyingPreferences = true;

        WidthSlider.Value = preferences.Width;
        OpacitySlider.Value = preferences.Opacity;
        WidthValueText.Text = $"{preferences.Width:0} px";
        OpacityValueText.Text = $"{preferences.Opacity:P0}";
        AnimationsCheckBox.IsChecked = preferences.AnimationsEnabled;
        AudioModuleCheckBox.IsChecked = preferences.ShowAudioModule;
        CaptureModuleCheckBox.IsChecked = preferences.ShowCaptureModule;
        SystemModuleCheckBox.IsChecked = preferences.ShowSystemModule;
        PeekEnabledCheckBox.IsChecked = preferences.PeekEnabled;
        PeekCpuCheckBox.IsChecked = preferences.ShowCpuInPeek;
        PeekMemoryCheckBox.IsChecked = preferences.ShowMemoryInPeek;
        PeekGpuCheckBox.IsChecked = preferences.ShowGpuInPeek;
        PeekDiskCheckBox.IsChecked = preferences.ShowDiskInPeek;
        PeekTopProcessCheckBox.IsChecked = preferences.ShowTopProcessInPeek;
        SaveConversationHistoryCheckBox.IsChecked = preferences.SaveConversationHistory;
        SpeakVoiceResponsesCheckBox.IsChecked = preferences.SpeakVoiceResponses;
        VoiceInputDeviceComboBox.SelectedValue = preferences.VoiceInputDeviceNumber;
        WakeWordEnabledCheckBox.IsChecked = preferences.WakeWordEnabled;
        WakeWordKohanaRadioButton.IsChecked = preferences.WakeWordPhrase is WakeWordPhrase.Kohana or WakeWordPhrase.Nexo;
        WakeWordOyeKohanaRadioButton.IsChecked = preferences.WakeWordPhrase is WakeWordPhrase.OyeKohana or WakeWordPhrase.OyeNexo;
        WakeWordHeyKohanaRadioButton.IsChecked = preferences.WakeWordPhrase is WakeWordPhrase.HeyKohana or WakeWordPhrase.HeyNexo;
        SelectWakeWordSensitivity(preferences.WakeWordSensitivity);
        SetWakeWordAliases(preferences.WakeWordAliases);
        ApplyAiProviderSelection(preferences.AiProvider);
        AiBaseUrlTextBox.Text = preferences.AiBaseUrl;
        AiModelTextBox.Text = preferences.AiModel;
        AiApiKeyVariableTextBox.Text = preferences.AiApiKeyEnvironmentVariable;
        ShareSystemMetricsWithAiCheckBox.IsChecked = preferences.ShareSystemMetricsWithAi;
        VisionEnabledCheckBox.IsChecked = preferences.VisionEnabled;
        ResourceGovernorEnabledCheckBox.IsChecked = preferences.ResourceGovernorEnabled;
        PauseWakeWordInGameModeCheckBox.IsChecked = preferences.PauseWakeWordInGameMode;
        ProtectVisionWhenBusyCheckBox.IsChecked = preferences.ProtectVisionWhenBusy;
        UpdateResourceGovernorOptionsAvailability();
        StartWithWindowsCheckBox.IsChecked = preferences.StartWithWindows;
        MinimizeToTrayCheckBox.IsChecked = preferences.MinimizeToTray;
        WindowsNotificationsCheckBox.IsChecked = preferences.ShowWindowsNotifications;
        NotificationSoundsCheckBox.IsChecked = preferences.PlayNotificationSounds;
        SetAiConnectionStatus(
            preferences.AiProvider == AiProviderKind.Disabled
                ? "La IA está desactivada."
                : $"{AiProviderDefaults.Get(preferences.AiProvider).DisplayName} configurado. Prueba la conexión antes de usarlo.",
            isSuccess: null);
        UpdatePositionButtons(preferences.Position);
        UpdatePeekOptionsAvailability();
        UpdateWakeWordOptionsAvailability();
        UpdateAiOptionsAvailability();

        _isApplyingPreferences = false;
    }

    private void LeftButton_Click(object sender, RoutedEventArgs e)
    {
        UpdatePositionButtons(SidebarPosition.Left);
        PositionChanged?.Invoke(SidebarPosition.Left);
    }

    private void RightButton_Click(object sender, RoutedEventArgs e)
    {
        UpdatePositionButtons(SidebarPosition.Right);
        PositionChanged?.Invoke(SidebarPosition.Right);
    }

    private void WidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (WidthValueText is null)
        {
            return;
        }

        WidthValueText.Text = $"{e.NewValue:0} px";
        if (!_isApplyingPreferences)
        {
            WidthChanged?.Invoke(e.NewValue);
        }
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityValueText is null)
        {
            return;
        }

        OpacityValueText.Text = $"{e.NewValue:P0}";
        if (!_isApplyingPreferences)
        {
            OpacityChanged?.Invoke(e.NewValue);
        }
    }

    private void AccentButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string accent })
        {
            AccentChanged?.Invoke(accent);
        }
    }

    private void AnimationsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isApplyingPreferences)
        {
            AnimationsChanged?.Invoke(AnimationsCheckBox.IsChecked == true);
        }
    }

    private void ModuleCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isApplyingPreferences || sender is not CheckBox { Tag: string module } checkBox)
        {
            return;
        }

        ModuleVisibilityChanged?.Invoke(module, checkBox.IsChecked == true);
    }

    private void PeekCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (PeekEnabledCheckBox is null)
        {
            return;
        }

        UpdatePeekOptionsAvailability();

        if (_isApplyingPreferences || sender is not CheckBox { Tag: string option } checkBox)
        {
            return;
        }

        PeekOptionChanged?.Invoke(option, checkBox.IsChecked == true);
    }

    private void SaveConversationHistoryCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isApplyingPreferences)
        {
            ConversationHistoryChanged?.Invoke(SaveConversationHistoryCheckBox.IsChecked == true);
        }
    }

    private void VisionEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isApplyingPreferences)
        {
            VisionEnabledChanged?.Invoke(VisionEnabledCheckBox.IsChecked == true);
        }
    }

    private void ResourceGovernorCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (ResourceGovernorEnabledCheckBox is null)
        {
            return;
        }

        UpdateResourceGovernorOptionsAvailability();
        if (_isApplyingPreferences)
        {
            return;
        }

        if (sender == ResourceGovernorEnabledCheckBox)
        {
            ResourceGovernorEnabledChanged?.Invoke(
                ResourceGovernorEnabledCheckBox.IsChecked == true);
        }
        else if (sender == PauseWakeWordInGameModeCheckBox)
        {
            PauseWakeWordInGameModeChanged?.Invoke(
                PauseWakeWordInGameModeCheckBox.IsChecked == true);
        }
        else if (sender == ProtectVisionWhenBusyCheckBox)
        {
            ProtectVisionWhenBusyChanged?.Invoke(
                ProtectVisionWhenBusyCheckBox.IsChecked == true);
        }
    }

    private void UpdateResourceGovernorOptionsAvailability()
    {
        if (PauseWakeWordInGameModeCheckBox is null ||
            ProtectVisionWhenBusyCheckBox is null)
        {
            return;
        }

        var enabled = ResourceGovernorEnabledCheckBox.IsChecked == true;
        PauseWakeWordInGameModeCheckBox.IsEnabled = enabled;
        ProtectVisionWhenBusyCheckBox.IsEnabled = enabled;
    }

    private void SpeakVoiceResponsesCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isApplyingPreferences)
        {
            VoiceResponsesChanged?.Invoke(SpeakVoiceResponsesCheckBox.IsChecked == true);
        }
    }

    private void StartWithWindowsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isApplyingPreferences)
        {
            StartWithWindowsChanged?.Invoke(StartWithWindowsCheckBox.IsChecked == true);
        }
    }

    private void MinimizeToTrayCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isApplyingPreferences)
        {
            MinimizeToTrayChanged?.Invoke(MinimizeToTrayCheckBox.IsChecked == true);
        }
    }

    private void WindowsNotificationsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isApplyingPreferences)
        {
            WindowsNotificationsChanged?.Invoke(WindowsNotificationsCheckBox.IsChecked == true);
        }
    }

    private void NotificationSoundsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isApplyingPreferences)
        {
            NotificationSoundsChanged?.Invoke(NotificationSoundsCheckBox.IsChecked == true);
        }
    }

    public void SetStartWithWindows(bool enabled)
    {
        if (StartWithWindowsCheckBox is null)
        {
            return;
        }

        _isApplyingPreferences = true;
        StartWithWindowsCheckBox.IsChecked = enabled;
        _isApplyingPreferences = false;
    }

    public void SetWindowsIntegrationStatus(string detail, bool? isSuccess)
    {
        if (WindowsIntegrationStatusText is null)
        {
            return;
        }

        WindowsIntegrationStatusText.Text = detail;
        WindowsIntegrationStatusText.Foreground = isSuccess switch
        {
            true => (System.Windows.Media.Brush)FindResource("BrushSuccess"),
            false => (System.Windows.Media.Brush)FindResource("BrushWarning"),
            _ => (System.Windows.Media.Brush)FindResource("BrushTextSecondary")
        };
    }

    public void SetVoiceInputDevices(
        IReadOnlyList<VoiceInputDevice> devices,
        int selectedDeviceNumber)
    {
        if (VoiceInputDeviceComboBox is null)
        {
            return;
        }

        _isApplyingPreferences = true;
        VoiceInputDeviceComboBox.ItemsSource = devices;
        VoiceInputDeviceComboBox.SelectedValue = selectedDeviceNumber;

        if (VoiceInputDeviceComboBox.SelectedItem is not VoiceInputDevice &&
            devices.Count > 0)
        {
            VoiceInputDeviceComboBox.SelectedIndex = 0;
        }

        VoiceInputDeviceComboBox.IsEnabled = devices.Count > 0;
        VoiceInputDeviceStatusText.Text = devices.Count switch
        {
            0 => "Windows no encontró micrófonos disponibles.",
            1 => "Se encontró un micrófono. Kohana lo usará para Mic y la frase de activación.",
            _ => "El micrófono elegido se usa tanto para Mic como para “Oye Kohana”."
        };
        _isApplyingPreferences = false;
    }

    private void VoiceInputDeviceComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_isApplyingPreferences ||
            VoiceInputDeviceComboBox.SelectedItem is not VoiceInputDevice device)
        {
            return;
        }

        VoiceInputDeviceChanged?.Invoke(device.DeviceNumber);
    }

    public void ApplyAiProviderDefaults(AiProviderKind provider)
    {
        var preset = AiProviderDefaults.Get(provider);
        _isApplyingPreferences = true;
        AiBaseUrlTextBox.Text = preset.BaseUrl;
        AiModelTextBox.Text = preset.DefaultModel;
        AiApiKeyVariableTextBox.Text = preset.ApiKeyEnvironmentVariable;
        _isApplyingPreferences = false;
        UpdateAiOptionsAvailability();
        SetAiConnectionStatus(
            provider == AiProviderKind.Disabled
                ? "La IA está desactivada."
                : $"{preset.DisplayName} seleccionado. Revisa el modelo y prueba la conexión.",
            isSuccess: null);
    }

    public void SetAiConnectionStatus(string detail, bool? isSuccess)
    {
        if (AiConnectionStatusText is null)
        {
            return;
        }

        AiConnectionStatusText.Text = detail;
        AiConnectionStatusText.Foreground = isSuccess switch
        {
            true => (System.Windows.Media.Brush)FindResource("BrushSuccess"),
            false => (System.Windows.Media.Brush)FindResource("BrushWarning"),
            _ => (System.Windows.Media.Brush)FindResource("BrushTextSecondary")
        };
    }

    public void SetAiModel(string model)
    {
        if (AiModelTextBox is null)
        {
            return;
        }

        _isApplyingPreferences = true;
        AiModelTextBox.Text = model;
        _isApplyingPreferences = false;
    }

    public void SetAiTestInProgress(bool inProgress)
    {
        if (AiTestConnectionButton is null)
        {
            return;
        }

        AiTestConnectionButton.IsEnabled = !inProgress &&
            AiDisabledRadioButton.IsChecked != true;
        AiTestConnectionButton.Content = inProgress
            ? "Probando…"
            : "Probar conexión";
        ManageModelsButton.IsEnabled = !inProgress &&
            AiOllamaRadioButton.IsChecked == true;
    }

    private void AiProviderRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (_isApplyingPreferences || sender is not RadioButton { Tag: string providerTag })
        {
            return;
        }

        var provider = ParseAiProvider(providerTag);
        AiProviderChanged?.Invoke(provider);
        ApplyAiProviderDefaults(provider);
    }

    private void AiTextBox_LostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        if (_isApplyingPreferences || sender is not TextBox { Tag: string field } textBox)
        {
            return;
        }

        var value = textBox.Text.Trim();
        switch (field)
        {
            case "BaseUrl":
                AiBaseUrlChanged?.Invoke(value);
                break;
            case "Model":
                AiModelChanged?.Invoke(value);
                break;
            case "ApiKeyVariable":
                AiApiKeyEnvironmentVariableChanged?.Invoke(value);
                break;
        }
    }

    private void ShareSystemMetricsWithAiCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isApplyingPreferences)
        {
            ShareSystemMetricsWithAiChanged?.Invoke(
                ShareSystemMetricsWithAiCheckBox.IsChecked == true);
        }
    }

    private void AiTestConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        AiBaseUrlChanged?.Invoke(AiBaseUrlTextBox.Text.Trim());
        AiModelChanged?.Invoke(AiModelTextBox.Text.Trim());
        AiApiKeyEnvironmentVariableChanged?.Invoke(AiApiKeyVariableTextBox.Text.Trim());
        AiTestConnectionRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ManageModelsButton_Click(object sender, RoutedEventArgs e)
    {
        AiBaseUrlChanged?.Invoke(AiBaseUrlTextBox.Text.Trim());
        AiModelChanged?.Invoke(AiModelTextBox.Text.Trim());
        ManageModelsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void DiagnosticsButton_Click(object sender, RoutedEventArgs e) =>
        DiagnosticsRequested?.Invoke(this, EventArgs.Empty);

    private void OnboardingButton_Click(object sender, RoutedEventArgs e) =>
        OnboardingRequested?.Invoke(this, EventArgs.Empty);

    private static AiProviderKind ParseAiProvider(string providerTag)
    {
        return Enum.TryParse<AiProviderKind>(providerTag, ignoreCase: true, out var provider)
            ? provider
            : AiProviderKind.Disabled;
    }

    private void ApplyAiProviderSelection(AiProviderKind provider)
    {
        AiDisabledRadioButton.IsChecked = provider == AiProviderKind.Disabled;
        AiOpenAiRadioButton.IsChecked = provider == AiProviderKind.OpenAI;
        AiOllamaRadioButton.IsChecked = provider == AiProviderKind.Ollama;
        AiLmStudioRadioButton.IsChecked = provider == AiProviderKind.LMStudio;
        AiCompatibleRadioButton.IsChecked = provider == AiProviderKind.OpenAICompatible;
    }

    private void UpdateAiOptionsAvailability()
    {
        if (AiBaseUrlTextBox is null)
        {
            return;
        }

        var enabled = AiDisabledRadioButton.IsChecked != true;
        AiBaseUrlTextBox.IsEnabled = enabled;
        AiModelTextBox.IsEnabled = enabled;
        AiApiKeyVariableTextBox.IsEnabled = enabled;
        ShareSystemMetricsWithAiCheckBox.IsEnabled = enabled;
        AiTestConnectionButton.IsEnabled = enabled;
        ManageModelsButton.IsEnabled = enabled && AiOllamaRadioButton.IsChecked == true;
    }

    private void WakeWordEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UpdateWakeWordOptionsAvailability();

        if (!_isApplyingPreferences)
        {
            WakeWordEnabledChanged?.Invoke(WakeWordEnabledCheckBox.IsChecked == true);
        }
    }

    private void WakeWordPhraseRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (_isApplyingPreferences || sender is not RadioButton { Tag: string phrase })
        {
            return;
        }

        var value = phrase.Equals("OyeKohana", StringComparison.OrdinalIgnoreCase)
            ? WakeWordPhrase.OyeKohana
            : phrase.Equals("HeyKohana", StringComparison.OrdinalIgnoreCase)
                ? WakeWordPhrase.HeyKohana
                : WakeWordPhrase.Kohana;
        WakeWordPhraseChanged?.Invoke(value);
    }

    private void WakeWordSensitivityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingPreferences ||
            WakeWordSensitivityComboBox.SelectedItem is not ComboBoxItem { Tag: string value } ||
            !Enum.TryParse<WakeWordSensitivity>(value, ignoreCase: true, out var sensitivity))
        {
            return;
        }

        WakeWordSensitivityChanged?.Invoke(sensitivity);
    }

    private void WakeWordTestButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isApplyingPreferences)
        {
            WakeWordTestRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetWakeWordTestStatus(string detail, bool? isSuccess)
    {
        WakeWordTestStatusText.Text = detail;
        WakeWordTestStatusText.Foreground = isSuccess switch
        {
            true => (System.Windows.Media.Brush)FindResource("BrushSuccess"),
            false => (System.Windows.Media.Brush)FindResource("BrushWarning"),
            _ => (System.Windows.Media.Brush)FindResource("BrushTextSecondary")
        };
    }

    public void SetWakeWordObservation(WakeWordRecognitionObservedEventArgs observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var state = observation.IsFinal ? "final" : "parcial";
        SetWakeWordTestStatus(
            $"Vosk ({state}) escuchó “{observation.RecognizedText}”. {observation.Match.Detail}",
            observation.Match.IsMatch ? true : null);
        WakeWordUseObservedAliasButton.IsEnabled =
            !observation.Match.IsMatch &&
            WakeWordAliasPolicy.TryNormalize(observation.RecognizedText, out _, out _);
    }

    public void SetWakeWordAliases(IReadOnlyCollection<string> aliases)
    {
        aliases ??= Array.Empty<string>();
        WakeWordAliasesText.Text = aliases.Count == 0
            ? "Aliases personales: ninguno"
            : "Aliases personales: " + string.Join(", ", aliases.Select(alias => $"“{alias}”"));
        WakeWordClearAliasesButton.IsEnabled = aliases.Count > 0;
    }

    public void ClearWakeWordObservation()
    {
        WakeWordUseObservedAliasButton.IsEnabled = false;
    }

    private void WakeWordUseObservedAliasButton_Click(object sender, RoutedEventArgs e) =>
        WakeWordAliasFromLastRequested?.Invoke(this, EventArgs.Empty);

    private void WakeWordClearAliasesButton_Click(object sender, RoutedEventArgs e) =>
        WakeWordAliasesClearRequested?.Invoke(this, EventArgs.Empty);

    private void SelectWakeWordSensitivity(WakeWordSensitivity sensitivity)
    {
        foreach (var item in WakeWordSensitivityComboBox.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag is string value &&
                Enum.TryParse<WakeWordSensitivity>(value, ignoreCase: true, out var parsed) &&
                parsed == sensitivity)
            {
                WakeWordSensitivityComboBox.SelectedItem = item;
                return;
            }
        }

        WakeWordSensitivityComboBox.SelectedIndex = 1;
    }

    private void UpdateWakeWordOptionsAvailability()
    {
        if (WakeWordKohanaRadioButton is null)
        {
            return;
        }

        var enabled = WakeWordEnabledCheckBox.IsChecked == true;
        WakeWordKohanaRadioButton.IsEnabled = enabled;
        WakeWordOyeKohanaRadioButton.IsEnabled = enabled;
        WakeWordHeyKohanaRadioButton.IsEnabled = enabled;
        WakeWordSensitivityComboBox.IsEnabled = enabled;
        WakeWordTestButton.IsEnabled = enabled;
        WakeWordUseObservedAliasButton.IsEnabled = false;
        WakeWordClearAliasesButton.IsEnabled = enabled &&
            !WakeWordAliasesText.Text.EndsWith("ninguno", StringComparison.OrdinalIgnoreCase);
    }

    private void UpdatePeekOptionsAvailability()
    {
        if (PeekCpuCheckBox is null)
        {
            return;
        }

        var enabled = PeekEnabledCheckBox.IsChecked == true;
        PeekCpuCheckBox.IsEnabled = enabled;
        PeekMemoryCheckBox.IsEnabled = enabled;
        PeekGpuCheckBox.IsEnabled = enabled;
        PeekDiskCheckBox.IsEnabled = enabled;
        PeekTopProcessCheckBox.IsEnabled = enabled;
    }

    private void UpdatePositionButtons(SidebarPosition position)
    {
        LeftButton.Background = position == SidebarPosition.Left
            ? (System.Windows.Media.Brush)FindResource("BrushAccentSoft")
            : (System.Windows.Media.Brush)FindResource("BrushSurfaceRaised");

        RightButton.Background = position == SidebarPosition.Right
            ? (System.Windows.Media.Brush)FindResource("BrushAccentSoft")
            : (System.Windows.Media.Brush)FindResource("BrushSurfaceRaised");
    }
}
