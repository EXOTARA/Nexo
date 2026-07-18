using System.IO;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Nexo.App.Views;
using Nexo.Core.Settings;
using Nexo.Windows.Settings;

namespace Nexo.App;

public partial class MainWindow : Window
{
    private const int HotkeyId = 0x4E58;
    private const uint ModAlt = 0x0001;
    private const uint VirtualKeyA = 0x41;
    private const int WmHotkey = 0x0312;

    private readonly DispatcherTimer _clockTimer;
    private readonly JsonSettingsStore _settingsStore = new();
    private readonly ShellPreferences _preferences;
    private readonly AssistantView _assistantView = new();
    private readonly AudioView _audioView = new();
    private readonly CaptureView _captureView = new();
    private readonly SystemView _systemView = new();
    private readonly SettingsView _settingsView = new();
    private readonly Dictionary<string, FrameworkElement> _views;

    private HwndSource? _windowSource;
    private bool _isHiding;
    private string _currentDestination = "Assistant";
    private string _previousDestination = "Assistant";

    public MainWindow()
    {
        InitializeComponent();

        _preferences = _settingsStore.Load();
        _views = new Dictionary<string, FrameworkElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["Assistant"] = _assistantView,
            ["Audio"] = _audioView,
            ["Capture"] = _captureView,
            ["System"] = _systemView,
            ["Settings"] = _settingsView
        };

        _assistantView.PromptSubmitted += AssistantView_PromptSubmitted;
        WireSettingsEvents();
        _settingsView.ApplyPreferences(_preferences);
        ApplyPreferences();
        NavigateTo("Assistant", animate: false);

        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => UpdateClock();
    }

    private void WireSettingsEvents()
    {
        _settingsView.PositionChanged += position =>
        {
            _preferences.Position = position;
            PositionWindow();
            SavePreferences();
        };

        _settingsView.WidthChanged += width =>
        {
            _preferences.Width = width;
            Width = width;
            PositionWindow();
            SavePreferences();
        };

        _settingsView.OpacityChanged += opacity =>
        {
            _preferences.Opacity = opacity;
            ApplyShellOpacity();
            SavePreferences();
        };

        _settingsView.AccentChanged += accent =>
        {
            _preferences.AccentColor = accent;
            ApplyAccent(accent);
            UpdateNavigationState(_currentDestination);
            SavePreferences();
        };

        _settingsView.AnimationsChanged += enabled =>
        {
            _preferences.AnimationsEnabled = enabled;
            SavePreferences();
        };

        _settingsView.ModuleVisibilityChanged += (module, visible) =>
        {
            SetModuleVisibility(module, visible);
            SavePreferences();
        };
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var windowHandle = new WindowInteropHelper(this).Handle;
        _windowSource = HwndSource.FromHwnd(windowHandle);
        _windowSource?.AddHook(WindowMessageHook);

        if (!RegisterHotKey(windowHandle, HotkeyId, ModAlt, VirtualKeyA))
        {
            _assistantView.AddNexoMessage("Alt + A ya está siendo utilizado por otra aplicación.");
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PositionWindow();
        UpdateClock();
        _clockTimer.Start();
        ShowAnimated();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _clockTimer.Stop();

        var windowHandle = new WindowInteropHelper(this).Handle;
        if (windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(windowHandle, HotkeyId);
        }

        _windowSource?.RemoveHook(WindowMessageHook);
    }

    private IntPtr WindowMessageHook(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            ToggleWindow();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void ToggleWindow()
    {
        if (IsVisible && Opacity > 0.1 && !_isHiding)
        {
            HideAnimated();
            return;
        }

        ShowAnimated();
    }

    private void ShowAnimated()
    {
        _isHiding = false;
        PositionWindow();

        if (!IsVisible)
        {
            Show();
        }

        Activate();
        Topmost = true;

        if (!_preferences.AnimationsEnabled)
        {
            ShellTranslate.X = 0;
            ShellBorder.Opacity = 1;
            FocusCurrentView();
            return;
        }

        var offset = _preferences.Position == SidebarPosition.Right ? 34 : -34;
        ShellTranslate.X = offset;
        ShellBorder.Opacity = 0;

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(170);

        ShellTranslate.BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation(0, duration) { EasingFunction = easing });

        ShellBorder.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(1, duration) { EasingFunction = easing });

        FocusCurrentView();
    }

    private void HideAnimated()
    {
        if (_isHiding)
        {
            return;
        }

        if (!_preferences.AnimationsEnabled)
        {
            Hide();
            return;
        }

        _isHiding = true;
        var offset = _preferences.Position == SidebarPosition.Right ? 34 : -34;
        var easing = new CubicEase { EasingMode = EasingMode.EaseIn };
        var duration = TimeSpan.FromMilliseconds(140);

        var slideAnimation = new DoubleAnimation(offset, duration)
        {
            EasingFunction = easing
        };

        var opacityAnimation = new DoubleAnimation(0, duration)
        {
            EasingFunction = easing
        };

        opacityAnimation.Completed += (_, _) =>
        {
            Hide();
            _isHiding = false;
        };

        ShellTranslate.BeginAnimation(TranslateTransform.XProperty, slideAnimation);
        ShellBorder.BeginAnimation(OpacityProperty, opacityAnimation);
    }

    private void PositionWindow()
    {
        var workArea = SystemParameters.WorkArea;
        Height = Math.Max(MinHeight, workArea.Height - 24);
        Top = workArea.Top + 12;
        Left = _preferences.Position == SidebarPosition.Right
            ? workArea.Right - Width - 12
            : workArea.Left + 12;
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        ClockText.Text = now.ToString("HH:mm", CultureInfo.InvariantCulture);
        DateText.Text = now.ToString("dddd, d 'de' MMMM", new CultureInfo("es-MX"));
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideAnimated();
            e.Handled = true;
        }
    }

    private void AssistantView_PromptSubmitted(object? sender, PromptSubmittedEventArgs e)
    {
        _assistantView.AddUserMessage(e.Prompt);
        _assistantView.AddNexoMessage("El motor de comandos e IA se conectará después de terminar la base modular.");
    }

    private void NavigationButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string destination })
        {
            NavigateTo(destination, animate: true);
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
{
    if (_currentDestination.Equals(
        "Settings",
        StringComparison.OrdinalIgnoreCase))
    {
        NavigateTo(_previousDestination, animate: true);
        return;
    }

    _previousDestination = _currentDestination;
    NavigateTo("Settings", animate: true);
}

    private void NavigateTo(string destination, bool animate)
    {
        if (!_views.TryGetValue(destination, out var view))
        {
            return;
        }

        _currentDestination = destination;
        ModuleHost.Content = view;
        UpdateNavigationState(destination);

        if (!animate || !_preferences.AnimationsEnabled)
        {
            ModuleHost.Opacity = 1;
            ModuleHost.RenderTransform = Transform.Identity;
            FocusCurrentView();
            return;
        }

        ModuleHost.Opacity = 0;
        var transform = new TranslateTransform(14, 0);
        ModuleHost.RenderTransform = transform;

        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(145);

        transform.BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation(0, duration) { EasingFunction = easing });

        ModuleHost.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(1, duration) { EasingFunction = easing });

        FocusCurrentView();
    }

    private void FocusCurrentView()
    {
        if (_currentDestination == "Assistant")
        {
            _assistantView.FocusPrompt();
        }
    }

    private void UpdateNavigationState(string destination)
    {
        var buttons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase)
        {
            ["Assistant"] = AssistantNavButton,
            ["Audio"] = AudioNavButton,
            ["Capture"] = CaptureNavButton,
            ["System"] = SystemNavButton
        };

        foreach (var pair in buttons)
        {
            var selected = pair.Key.Equals(destination, StringComparison.OrdinalIgnoreCase);
            pair.Value.Background = selected
                ? (Brush)FindResource("BrushAccentSoft")
                : Brushes.Transparent;
            pair.Value.Foreground = selected
                ? (Brush)FindResource("BrushTextPrimary")
                : (Brush)FindResource("BrushTextSecondary");
        }

        SettingsButton.Background = destination.Equals("Settings", StringComparison.OrdinalIgnoreCase)
            ? (Brush)FindResource("BrushAccentSoft")
            : (Brush)FindResource("BrushSurfaceRaised");
    }

    private void ApplyPreferences()
    {
        _preferences.Normalize();
        Width = _preferences.Width;
        ApplyShellOpacity();
        ApplyAccent(_preferences.AccentColor);
        ApplyModuleVisibility();
    }

    private void ApplyShellOpacity()
    {
        var baseColor = (Color)ColorConverter.ConvertFromString("#11131A");
        var alpha = (byte)Math.Round(_preferences.Opacity * 255);
        ShellBorder.Background = new SolidColorBrush(Color.FromArgb(
            alpha,
            baseColor.R,
            baseColor.G,
            baseColor.B));
    }

    private static void ApplyAccent(string accentHex)
    {
        try
        {
            var accent = (Color)ColorConverter.ConvertFromString(accentHex);
            var soft = Color.FromArgb(255,
                (byte)(accent.R * 0.24),
                (byte)(accent.G * 0.22),
                (byte)(accent.B * 0.34));

            Application.Current.Resources["BrushAccent"] = new SolidColorBrush(accent);
            Application.Current.Resources["BrushAccentSoft"] = new SolidColorBrush(soft);
        }
        catch (Exception exception) when (exception is FormatException or NotSupportedException)
        {
            Application.Current.Resources["BrushAccent"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B6CFF"));
            Application.Current.Resources["BrushAccentSoft"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2748"));
        }
    }

    private void ApplyModuleVisibility()
    {
        AudioNavButton.Visibility = _preferences.ShowAudioModule ? Visibility.Visible : Visibility.Collapsed;
        CaptureNavButton.Visibility = _preferences.ShowCaptureModule ? Visibility.Visible : Visibility.Collapsed;
        SystemNavButton.Visibility = _preferences.ShowSystemModule ? Visibility.Visible : Visibility.Collapsed;
        UpdateNavigationColumns();
    }

    private void SetModuleVisibility(string module, bool visible)
    {
        switch (module)
        {
            case "Audio":
                _preferences.ShowAudioModule = visible;
                break;
            case "Capture":
                _preferences.ShowCaptureModule = visible;
                break;
            case "System":
                _preferences.ShowSystemModule = visible;
                break;
        }

        ApplyModuleVisibility();

        if (!visible && _currentDestination.Equals(module, StringComparison.OrdinalIgnoreCase))
        {
            NavigateTo("Assistant", animate: true);
        }
    }

    private void UpdateNavigationColumns()
    {
        var visibleCount = 1;
        visibleCount += AudioNavButton.Visibility == Visibility.Visible ? 1 : 0;
        visibleCount += CaptureNavButton.Visibility == Visibility.Visible ? 1 : 0;
        visibleCount += SystemNavButton.Visibility == Visibility.Visible ? 1 : 0;
        NavigationGrid.Columns = visibleCount;
    }

    private void SavePreferences()
    {
        try
        {
            _settingsStore.Save(_preferences);
        }
        catch (IOException)
        {
            _assistantView.AddNexoMessage("No se pudo guardar la configuración en este momento.");
        }
        catch (UnauthorizedAccessException)
        {
            _assistantView.AddNexoMessage("Windows no permitió guardar la configuración.");
        }
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        HideAnimated();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(
        IntPtr windowHandle,
        int id,
        uint modifiers,
        uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr windowHandle, int id);
}
