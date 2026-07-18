using System.Windows;
using System.Windows.Controls;
using Nexo.Core.Settings;

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
        UpdatePositionButtons(preferences.Position);
        UpdatePeekOptionsAvailability();

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
