using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Nexo.App.Views;
using Nexo.Core.Commands;
using Nexo.Core.Metrics;
using Nexo.Core.Settings;
using Nexo.Windows.Assistant;
using Nexo.Windows.Metrics;
using Nexo.Windows.Settings;

namespace Nexo.App;

public partial class MainWindow : Window
{
    private const int ShellHotkeyId = 0x4E58;
    private const int PeekHotkeyId = 0x4E59;
    private const uint ModAlt = 0x0001;
    private const uint ModShift = 0x0004;
    private const uint VirtualKeyA = 0x41;
    private const int WmHotkey = 0x0312;

    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _metricsTimer;
    private readonly JsonSettingsStore _settingsStore = new();
    private readonly JsonConversationStore _conversationStore = new();
    private readonly NaturalCommandParser _commandParser = new();
    private readonly WindowsSystemMetricsService _metricsService = new();
    private readonly ShellPreferences _preferences;
    private readonly AssistantView _assistantView = new();
    private readonly AudioView _audioView = new();
    private readonly CaptureView _captureView = new();
    private readonly SystemView _systemView = new();
    private readonly SettingsView _settingsView = new();
    private readonly PeekWindow _peekWindow = new();
    private readonly CapsuleWindow _capsuleWindow = new();
    private readonly Dictionary<string, FrameworkElement> _views;

    private HwndSource? _windowSource;
    private SystemSnapshot _latestSnapshot = SystemSnapshot.Empty;
    private bool _isHiding;
    private bool _isClosed;
    private int _metricsRefreshInProgress;
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
        _assistantView.ConversationChanged += AssistantView_ConversationChanged;
        _assistantView.ConversationCleared += AssistantView_ConversationCleared;
        _assistantView.ConfigureHistory(
            _preferences.SaveConversationHistory,
            _preferences.RecentConversationMessageLimit);

        if (_preferences.SaveConversationHistory)
        {
            _assistantView.LoadConversation(_conversationStore.Load());
        }

        WireSettingsEvents();
        _settingsView.ApplyPreferences(_preferences);
        ApplyPreferences();
        NavigateTo("Assistant", animate: false);

        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => UpdateClock();

        _metricsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _metricsTimer.Tick += async (_, _) => await RefreshMetricsAsync();
    }

    private void WireSettingsEvents()
    {
        _settingsView.PositionChanged += position =>
        {
            _preferences.Position = position;
            PositionWindow();
            _peekWindow.HideImmediately();
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

        _settingsView.PeekOptionChanged += (option, enabled) =>
        {
            ApplyPeekOption(option, enabled);
            SavePreferences();
        };

        _settingsView.ConversationHistoryChanged += enabled =>
        {
            _preferences.SaveConversationHistory = enabled;
            _assistantView.ConfigureHistory(
                enabled,
                _preferences.RecentConversationMessageLimit);

            if (enabled)
            {
                _conversationStore.Save(_assistantView.GetConversationSnapshot());
            }
            else
            {
                _conversationStore.Clear();
            }

            SavePreferences();
        };
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var windowHandle = new WindowInteropHelper(this).Handle;
        _windowSource = HwndSource.FromHwnd(windowHandle);
        _windowSource?.AddHook(WindowMessageHook);

        if (!RegisterHotKey(windowHandle, ShellHotkeyId, ModAlt, VirtualKeyA))
        {
            _assistantView.AddNexoMessage("Alt + A ya está siendo utilizado por otra aplicación.");
        }

        if (!RegisterHotKey(windowHandle, PeekHotkeyId, ModAlt | ModShift, VirtualKeyA))
        {
            _assistantView.AddNexoMessage("Alt + Shift + A ya está siendo utilizado por otra aplicación.");
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PositionWindow();
        UpdateClock();
        _clockTimer.Start();
        SetMetricsCadence(isShellVisible: true);
        _metricsTimer.Start();
        ShowAnimated();
        await RefreshMetricsAsync();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _isClosed = true;
        _clockTimer.Stop();
        _metricsTimer.Stop();
        _peekWindow.HideImmediately();
        _capsuleWindow.HideImmediately();

        if (_preferences.SaveConversationHistory)
        {
            _conversationStore.Save(_assistantView.GetConversationSnapshot());
        }

        _capsuleWindow.Close();

        var windowHandle = new WindowInteropHelper(this).Handle;
        if (windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(windowHandle, ShellHotkeyId);
            UnregisterHotKey(windowHandle, PeekHotkeyId);
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
        if (message != WmHotkey)
        {
            return IntPtr.Zero;
        }

        if (wParam.ToInt32() == ShellHotkeyId)
        {
            ToggleWindow();
            handled = true;
        }
        else if (wParam.ToInt32() == PeekHotkeyId)
        {
            _ = ShowPeekAsync();
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
        SetMetricsCadence(isShellVisible: true);
        _ = RefreshMetricsAsync();

        if (!IsVisible)
        {
            Show();
        }

        Activate();
        Topmost = true;

        ShellBorder.BeginAnimation(OpacityProperty, null);
        ShellTranslate.BeginAnimation(TranslateTransform.XProperty, null);

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
            SetMetricsCadence(isShellVisible: false);
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
            SetMetricsCadence(isShellVisible: false);
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

    private void AssistantView_ConversationChanged(object? sender, EventArgs e)
    {
        if (_preferences.SaveConversationHistory)
        {
            _conversationStore.Save(_assistantView.GetConversationSnapshot());
        }
    }

    private void AssistantView_ConversationCleared(object? sender, EventArgs e)
    {
        _conversationStore.Clear();
    }

    private async void AssistantView_PromptSubmitted(object? sender, PromptSubmittedEventArgs e)
    {
        _assistantView.AddUserMessage(e.Prompt);
        _capsuleWindow.ShowMessage(
            CapsuleKind.Processing,
            "Nexo está procesando",
            e.Prompt,
            _preferences.Position);

        await Task.Yield();

        var interpretation = _commandParser.Parse(e.Prompt);
        if (interpretation.Route == CommandRoute.ArtificialIntelligence || interpretation.Intent is null)
        {
            _assistantView.AddNexoMessage(
                "Entendí que es una consulta abierta. El proveedor de IA se conectará en un sprint posterior; por ahora no envié nada fuera de tu equipo.");
            _capsuleWindow.ShowMessage(
                CapsuleKind.Information,
                "Consulta preparada",
                "Todavía no hay un proveedor de IA conectado.",
                _preferences.Position);
            return;
        }

        await ExecuteLocalCommandAsync(interpretation.Intent);
    }

    private async Task ExecuteLocalCommandAsync(LocalCommandIntent intent)
    {
        switch (intent.Type)
        {
            case LocalCommandType.ShowPeek:
                if (!_preferences.PeekEnabled)
                {
                    _assistantView.AddNexoMessage("La vista Peek está desactivada en Personalización.");
                    _capsuleWindow.ShowMessage(
                        CapsuleKind.Warning,
                        "Peek está desactivado",
                        "Puedes activarlo desde Personalización.",
                        _preferences.Position);
                    break;
                }

                await ShowPeekAsync();
                ShowCommandSuccess("Vista rápida abierta", "Peek muestra el estado actual del equipo.");
                break;

            case LocalCommandType.ShowSystemStatus:
                await ShowSystemStatusAsync();
                break;

            case LocalCommandType.NavigateAssistant:
                ShowShellModule("Assistant", "Asistente abierto");
                break;

            case LocalCommandType.NavigateAudio:
                ShowShellModule("Audio", "Audio abierto");
                break;

            case LocalCommandType.NavigateCapture:
                ShowShellModule("Capture", "Captura abierta");
                break;

            case LocalCommandType.NavigateSystem:
                ShowShellModule("System", "Sistema abierto");
                break;

            case LocalCommandType.NavigateSettings:
                ShowShellModule("Settings", "Ajustes abiertos");
                break;

            case LocalCommandType.OpenPowerShell:
                OpenShell("powershell.exe", "-NoExit", "PowerShell abierto");
                break;

            case LocalCommandType.OpenCommandPrompt:
                OpenShell("cmd.exe", string.Empty, "CMD abierto");
                break;

            case LocalCommandType.OpenWindowsTerminal:
                OpenShell("wt.exe", string.Empty, "Terminal abierta");
                break;

            case LocalCommandType.SetApplicationVolume:
            case LocalCommandType.ScaleApplicationVolume:
            case LocalCommandType.ChangeApplicationVolume:
            case LocalCommandType.MuteApplication:
            case LocalCommandType.UnmuteApplication:
            case LocalCommandType.LowerAllExcept:
                ShowPendingAudioCommand(intent);
                break;

            default:
                _assistantView.AddNexoMessage("No pude ejecutar esa orden local todavía.");
                _capsuleWindow.ShowMessage(
                    CapsuleKind.Warning,
                    "Comando no disponible",
                    "La orden fue reconocida, pero aún no tiene una acción conectada.",
                    _preferences.Position);
                break;
        }
    }

    private async Task ShowSystemStatusAsync()
    {
        var snapshotAge = DateTimeOffset.Now - _latestSnapshot.CapturedAt;
        if (_latestSnapshot.CapturedAt == DateTimeOffset.MinValue || snapshotAge > TimeSpan.FromSeconds(4))
        {
            await RefreshMetricsAsync();
        }

        var topProcess = string.IsNullOrWhiteSpace(_latestSnapshot.TopProcessName)
            ? "sin proceso destacado"
            : $"{_latestSnapshot.TopProcessName} · {(_latestSnapshot.TopProcessWorkingSetBytes.GetValueOrDefault() / 1024d / 1024d):0} MB";

        var summary =
            $"CPU {FormatPercentage(_latestSnapshot.CpuUsagePercent)} · " +
            $"RAM {FormatPercentage(_latestSnapshot.MemoryUsagePercent)} · " +
            $"GPU {FormatPercentage(_latestSnapshot.GpuUsagePercent)}. " +
            $"Mayor uso de memoria: {topProcess}.";

        _assistantView.AddNexoMessage(summary);
        _capsuleWindow.ShowMessage(
            CapsuleKind.Success,
            "Estado del equipo listo",
            $"CPU {FormatPercentage(_latestSnapshot.CpuUsagePercent)} · RAM {FormatPercentage(_latestSnapshot.MemoryUsagePercent)} · GPU {FormatPercentage(_latestSnapshot.GpuUsagePercent)}",
            _preferences.Position);
    }

    private void ShowShellModule(string destination, string confirmation)
    {
        ShowAnimated();
        NavigateTo(destination, animate: true);
        ShowCommandSuccess(confirmation, "La orden se ejecutó localmente.");
    }

    private void OpenShell(string fileName, string arguments, string confirmation)
    {
        try
        {
            var userFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = userFolder,
                UseShellExecute = true
            });

            _assistantView.AddNexoMessage($"{confirmation} en {userFolder}.");
            ShowCommandSuccess(confirmation, userFolder);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _assistantView.AddNexoMessage($"No pude abrir {fileName}.");
            _capsuleWindow.ShowMessage(
                CapsuleKind.Error,
                "No se pudo abrir",
                fileName,
                _preferences.Position);
        }
    }

    private void ShowPendingAudioCommand(LocalCommandIntent intent)
    {
        var target = string.IsNullOrWhiteSpace(intent.Target) ? "la aplicación" : ToDisplayName(intent.Target);
        var detail = intent.Type switch
        {
            LocalCommandType.SetApplicationVolume => $"{target} al {intent.Percent:0}%",
            LocalCommandType.ScaleApplicationVolume => $"{target} a la mitad de su volumen actual",
            LocalCommandType.ChangeApplicationVolume => $"subir {target} {intent.DeltaPoints:0} puntos",
            LocalCommandType.MuteApplication => $"silenciar {target}",
            LocalCommandType.UnmuteApplication => $"quitar el silencio de {target}",
            LocalCommandType.LowerAllExcept => $"bajar todo excepto {target}",
            _ => "ajustar una sesión de audio"
        };

        _assistantView.AddNexoMessage(
            $"Reconocí la orden para {detail}. El mezclador por aplicación se conectará en el siguiente bloque.");
        _capsuleWindow.ShowMessage(
            CapsuleKind.Warning,
            "Comando de audio reconocido",
            detail,
            _preferences.Position);
    }

    private void ShowCommandSuccess(string title, string detail)
    {
        _capsuleWindow.ShowMessage(
            CapsuleKind.Success,
            title,
            detail,
            _preferences.Position);
    }

    private static string ToDisplayName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return char.ToUpperInvariant(value[0]) + value[1..];
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
        if (_currentDestination.Equals("Settings", StringComparison.OrdinalIgnoreCase))
        {
            NavigateTo(_previousDestination, animate: true);
            return;
        }

        _previousDestination = _currentDestination;
        NavigateTo("Settings", animate: true);
    }

    private async void PeekButton_Click(object sender, RoutedEventArgs e)
    {
        await ShowPeekAsync();
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
            var soft = Color.FromArgb(
                255,
                (byte)(accent.R * 0.24),
                (byte)(accent.G * 0.22),
                (byte)(accent.B * 0.34));

            Application.Current.Resources["BrushAccent"] = new SolidColorBrush(accent);
            Application.Current.Resources["BrushAccentSoft"] = new SolidColorBrush(soft);
        }
        catch (Exception exception) when (exception is FormatException or NotSupportedException)
        {
            Application.Current.Resources["BrushAccent"] = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#8B6CFF"));
            Application.Current.Resources["BrushAccentSoft"] = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#2D2748"));
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

    private void ApplyPeekOption(string option, bool enabled)
    {
        switch (option)
        {
            case "Enabled":
                _preferences.PeekEnabled = enabled;
                if (!enabled)
                {
                    _peekWindow.HideImmediately();
                }
                break;
            case "Cpu":
                _preferences.ShowCpuInPeek = enabled;
                break;
            case "Memory":
                _preferences.ShowMemoryInPeek = enabled;
                break;
            case "Gpu":
                _preferences.ShowGpuInPeek = enabled;
                break;
            case "Disk":
                _preferences.ShowDiskInPeek = enabled;
                break;
            case "TopProcess":
                _preferences.ShowTopProcessInPeek = enabled;
                break;
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

    private void SetMetricsCadence(bool isShellVisible)
    {
        _metricsTimer.Interval = isShellVisible
            ? TimeSpan.FromSeconds(2)
            : TimeSpan.FromSeconds(8);
    }

    private async Task RefreshMetricsAsync()
    {
        if (Interlocked.Exchange(ref _metricsRefreshInProgress, 1) == 1)
        {
            return;
        }

        try
        {
            var snapshot = await Task.Run(_metricsService.ReadSnapshot);
            if (_isClosed)
            {
                return;
            }

            _latestSnapshot = snapshot;
            UpdateMetricControls(snapshot);
        }
        catch (Exception)
        {
            // Las métricas son informativas: un fallo de lectura nunca debe cerrar Nexo.
        }
        finally
        {
            Interlocked.Exchange(ref _metricsRefreshInProgress, 0);
        }
    }

    private void UpdateMetricControls(SystemSnapshot snapshot)
    {
        HeaderCpuText.Text = FormatPercentage(snapshot.CpuUsagePercent);
        HeaderMemoryText.Text = FormatPercentage(snapshot.MemoryUsagePercent);
        HeaderGpuText.Text = FormatPercentage(snapshot.GpuUsagePercent);
        _systemView.UpdateSnapshot(snapshot);
    }

    private async Task ShowPeekAsync()
    {
        if (!_preferences.PeekEnabled)
        {
            _assistantView.AddNexoMessage("La vista Peek está desactivada en Personalización.");
            return;
        }

        var snapshotAge = DateTimeOffset.Now - _latestSnapshot.CapturedAt;
        if (_latestSnapshot.CapturedAt == DateTimeOffset.MinValue || snapshotAge > TimeSpan.FromSeconds(5))
        {
            await RefreshMetricsAsync();
        }

        _peekWindow.ShowSnapshot(_latestSnapshot, _preferences);
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

    private static string FormatPercentage(double? value)
    {
        return value.HasValue ? $"{value.Value:0}%" : "—";
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
