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
    public event Action<bool>? WakeWordEnabledChanged;
    public event Action<WakeWordPhrase>? WakeWordPhraseChanged;
    public event Action<AiProviderKind>? AiProviderChanged;
    public event Action<string>? AiBaseUrlChanged;
    public event Action<string>? AiModelChanged;
    public event Action<string>? AiApiKeyEnvironmentVariableChanged;
    public event Action<bool>? ShareSystemMetricsWithAiChanged;
    public event EventHandler? AiTestConnectionRequested;

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
        WakeWordEnabledCheckBox.IsChecked = preferences.WakeWordEnabled;
        WakeWordNexoRadioButton.IsChecked = preferences.WakeWordPhrase == WakeWordPhrase.Nexo;
        WakeWordOyeNexoRadioButton.IsChecked = preferences.WakeWordPhrase == WakeWordPhrase.OyeNexo;
        ApplyAiProviderSelection(preferences.AiProvider);
        AiBaseUrlTextBox.Text = preferences.AiBaseUrl;
        AiModelTextBox.Text = preferences.AiModel;
        AiApiKeyVariableTextBox.Text = preferences.AiApiKeyEnvironmentVariable;
        ShareSystemMetricsWithAiCheckBox.IsChecked = preferences.ShareSystemMetricsWithAi;
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

    private void SpeakVoiceResponsesCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isApplyingPreferences)
        {
            VoiceResponsesChanged?.Invoke(SpeakVoiceResponsesCheckBox.IsChecked == true);
        }
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

        var value = phrase.Equals("OyeNexo", StringComparison.OrdinalIgnoreCase)
            ? WakeWordPhrase.OyeNexo
            : WakeWordPhrase.Nexo;
        WakeWordPhraseChanged?.Invoke(value);
    }

    private void UpdateWakeWordOptionsAvailability()
    {
        if (WakeWordNexoRadioButton is null)
        {
            return;
        }

        var enabled = WakeWordEnabledCheckBox.IsChecked == true;
        WakeWordNexoRadioButton.IsEnabled = enabled;
        WakeWordOyeNexoRadioButton.IsEnabled = enabled;
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
