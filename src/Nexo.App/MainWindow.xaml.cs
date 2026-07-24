using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Nexo.App.Automation;
using Nexo.App.WindowsIntegration;
using Nexo.App.Views;
using Nexo.Core.Ai;
using Nexo.Core.Automation;
using Nexo.Core.Audio;
using Nexo.Core.Commands;
using Nexo.Core.Focus;
using Nexo.Core.Metrics;
using Nexo.Core.Resources;
using Nexo.Core.Settings;
using Nexo.Core.Shell;
using Nexo.Core.Tasks;
using Nexo.Core.Voice;
using Nexo.Core.Vision;
using Nexo.Windows.Ai;
using Nexo.Windows.Automation;
using Nexo.Windows.Assistant;
using Nexo.Windows.Audio;
using Nexo.Windows.Focus;
using Nexo.Windows.Metrics;
using Nexo.Windows.Resources;
using Nexo.Windows.Settings;
using Nexo.Windows.Tasks;
using Nexo.Windows.Voice;
using Nexo.Windows.Vision;
using Nexo.Windows.WindowsIntegration;
using NexoFocusManager = Nexo.Core.Focus.FocusManager;

namespace Nexo.App;

public partial class MainWindow : Window
{
    private const int ShellHotkeyId = 0x4E58;
    private const int PeekHotkeyId = 0x4E59;
    private const int CommandPaletteHotkeyId = 0x4E5A;
    private const int LookHotkeyId = 0x4E5B;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint VirtualKeyA = 0x41;
    private const uint VirtualKeySpace = 0x20;
    private const int WmHotkey = 0x0312;
    private const int WmPowerBroadcast = 0x0218;
    private const int PbtApmResumeSuspend = 0x0007;
    private const int PbtApmResumeAutomatic = 0x0012;

    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _metricsTimer;
    private readonly DispatcherTimer _taskReminderTimer;
    private readonly DispatcherTimer _focusTickTimer;
    private readonly DispatcherTimer _visualContextExpiryTimer = new();
    private readonly JsonSettingsStore _settingsStore = new();
    private readonly WindowsStartupService _startupService = new();
    private readonly JsonConversationStore _conversationStore = new();
    private readonly NaturalCommandParser _commandParser = new();
    private readonly SpanishTaskCommandParser _taskCommandParser = new();
    private readonly SpanishFocusCommandParser _focusCommandParser = new();
    private readonly SpanishRoutineCommandParser _routineCommandParser = new();
    private readonly IAiChatService _aiChatService;
    private readonly IAudioMixerService _audioMixerService;
    private readonly IVoiceInputService _voiceInputService;
    private readonly IVoiceOutputService _voiceOutputService;
    private readonly IWakeWordService _wakeWordService;
    private readonly IScreenCaptureService _screenCaptureService;
    private readonly VoiceCoordinator _voiceCoordinator;
    private readonly SemaphoreSlim _voiceGate = new(1, 1);
    private readonly SemaphoreSlim _wakeWordGate = new(1, 1);
    private readonly SemaphoreSlim _aiGate = new(1, 1);
    private readonly SemaphoreSlim _resourceGovernorVoiceGate = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly WindowsSystemMetricsService _metricsService = new();
    private readonly WindowsResourceGovernorService _resourceGovernorService = new();
    private readonly ShellPreferences _preferences;
    private readonly JsonTaskStore _taskStore = new();
    private readonly TaskManager _taskManager;
    private readonly JsonFocusStore _focusStore = new();
    private readonly NexoFocusManager _focusManager;
    private readonly JsonRoutineStore _routineStore = new();
    private readonly RoutineManager _routineManager;
    private readonly RoutineRunner _routineRunner;
    private readonly HomeView _homeView = new();
    private readonly AssistantView _assistantView = new();
    private readonly TasksView _tasksView;
    private readonly FocusView _focusView;
    private readonly RoutinesView _routinesView;
    private readonly AudioView _audioView;
    private readonly CaptureView _captureView = new();
    private readonly SystemView _systemView = new();
    private readonly SettingsView _settingsView = new();
    private readonly PeekWindow _peekWindow = new();
    private readonly CapsuleWindow _capsuleWindow = new();
    private readonly CommandPaletteWindow _commandPaletteWindow;
    private readonly TrayIconController _trayIcon;
    private readonly Dictionary<string, FrameworkElement> _views;
    private readonly bool _startHidden;
    private readonly ManagedOllamaSupervisor? _managedOllamaSupervisor;

    private HwndSource? _windowSource;
    private SystemSnapshot _latestSnapshot = SystemSnapshot.Empty;
    private ResourceGovernorDecision _resourceDecision = ResourceGovernorDecision.Normal;
    private bool _isHiding;
    private bool _isClosed;
    private bool _allowExit;
    private bool _trayHintShown;
    private int _metricsRefreshInProgress;
    private string _currentDestination = "Home";
    private string _previousDestination = "Home";
    private bool _voicePromptActive;
    private bool _managedAiRuntimeFailureNotified;
    private bool _promptFromCommandPalette;
    private bool _sideRailExpanded;
    private bool _visualContextPersistent;
    private bool _silentVisualContext;
    private bool _resourceGovernorWakeWordPaused;
    private bool _wakeWordTestActive;
    private CancellationTokenSource? _wakeWordTestCancellation;
    private WakeWordRecognitionObservedEventArgs? _lastWakeWordObservation;
    private string _runtimeAiStatus = "Desactivada";
    private bool _runtimeAiHealthy;
    private string? _visualContextMetadata;
    private string? _pendingVoicePrompt;
    private AiImageAttachment? _pendingVisionAttachment;
    private long _lastExternalWindowHandle;

    public MainWindow(
        bool startHidden = false,
        ManagedOllamaSupervisor? managedOllamaSupervisor = null,
        IAiChatService? aiChatService = null,
        IAudioMixerService? audioMixerService = null,
        IVoiceInputService? voiceInputService = null,
        IVoiceOutputService? voiceOutputService = null,
        IWakeWordService? wakeWordService = null,
        IScreenCaptureService? screenCaptureService = null,
        VoiceCoordinator? voiceCoordinator = null)
    {
        InitializeComponent();
        _commandPaletteWindow = new CommandPaletteWindow();

        // Mismo orden relativo que los inicializadores de campo que sustituyen: se resuelven
        // aquí, antes de cualquier otro campo dependiente y antes de cablear eventos, igual que
        // ocurría cuando eran `= new ...()`. Los valores por defecto solo cubren la construcción
        // directa en pruebas; App.OnStartup siempre los provee desde KohanaCompositionRoot.
        _aiChatService = aiChatService ?? new AiChatRouterService();
        _audioMixerService = audioMixerService ?? new WindowsAudioMixerService();
        _voiceInputService = voiceInputService ?? new WhisperVoiceInputService();
        _voiceOutputService = voiceOutputService ?? new WindowsTextToSpeechService();
        _wakeWordService = wakeWordService ?? new VoskWakeWordService();
        _screenCaptureService = screenCaptureService ?? new WindowsScreenCaptureService();

        // Fase 1.3B1: el coordinador se agrega al final de la firma para minimizar el
        // cambio. Si no se provee (construcción directa fuera de App.OnStartup), envuelve
        // los mismos tres campos ya resueltos arriba — nunca construye un cuarto motor.
        // MainWindow sigue recibiendo y liberando _voiceInputService/_voiceOutputService/
        // _wakeWordService directamente: solo se usa el coordinador donde 1.3B1 migra.
        _voiceCoordinator = voiceCoordinator
            ?? new VoiceCoordinator(_voiceInputService, _voiceOutputService, _wakeWordService);

        _startHidden = startHidden;
        _managedOllamaSupervisor = managedOllamaSupervisor;
        _preferences = _settingsStore.Load();
        _preferences.StartWithWindows = _startupService.IsEnabled();
        _taskManager = new TaskManager(_taskStore);
        _taskManager.Load();
        _focusManager = new NexoFocusManager(_focusStore);
        _focusManager.Load();
        _routineManager = new RoutineManager(_routineStore);
        _routineManager.Load();
        _routineRunner = new RoutineRunner(new NexoAutomationActionExecutor(
            _audioMixerService,
            _focusManager,
            _taskManager));
        _tasksView = new TasksView(_taskManager);
        _focusView = new FocusView(_focusManager);
        _routinesView = new RoutinesView(_routineManager);
        _audioView = new AudioView(_audioMixerService);
        _views = new Dictionary<string, FrameworkElement>(StringComparer.OrdinalIgnoreCase)
        {
            [ShellNavigationPolicy.Home] = _homeView,
            [ShellNavigationPolicy.Assistant] = _assistantView,
            [ShellNavigationPolicy.Tasks] = _tasksView,
            [ShellNavigationPolicy.Focus] = _focusView,
            [ShellNavigationPolicy.Routines] = _routinesView,
            [ShellNavigationPolicy.Audio] = _audioView,
            [ShellNavigationPolicy.Capture] = _captureView,
            [ShellNavigationPolicy.System] = _systemView,
            [ShellNavigationPolicy.Settings] = _settingsView
        };

        _trayIcon = new TrayIconController(
            () => Dispatcher.BeginInvoke(new Action(ShowFromBackground)),
            () => Dispatcher.BeginInvoke(new Action(async () => await ShowPeekAsync())),
            () => Dispatcher.BeginInvoke(new Action(RequestExit)));

        _assistantView.PromptSubmitted += AssistantView_PromptSubmitted;
        _assistantView.ConversationChanged += AssistantView_ConversationChanged;
        _assistantView.ConversationCleared += AssistantView_ConversationCleared;
        _assistantView.VoiceInputStarted += AssistantView_VoiceInputStarted;
        _assistantView.VoiceInputStopped += AssistantView_VoiceInputStopped;
        _assistantView.VisionCaptureRequested += AssistantView_VisionCaptureRequested;
        _assistantView.VisionAttachmentCleared += AssistantView_VisionAttachmentCleared;
        _tasksView.TasksChanged += TasksView_TasksChanged;
        _focusView.FocusChanged += FocusView_FocusChanged;
        _routinesView.ExecuteRequested += RoutinesView_ExecuteRequested;
        _wakeWordService.WakeWordDetected += WakeWordService_WakeWordDetected;
        _wakeWordService.RecognitionObserved += WakeWordService_RecognitionObserved;
        _wakeWordService.CustomAliases = _preferences.WakeWordAliases;
        _audioView.ActionCompleted += AudioView_ActionCompleted;
        _captureView.CaptureRequested += CaptureView_CaptureRequested;
        _commandPaletteWindow.PromptSubmitted += CommandPaletteWindow_PromptSubmitted;
        _commandPaletteWindow.WorkspaceRequested += CommandPaletteWindow_WorkspaceRequested;
        _homeView.CommandRequested += HomeView_CommandRequested;
        _homeView.TasksRequested += HomeView_TasksRequested;
        _homeView.FocusRequested += HomeView_FocusRequested;
        _homeView.ContextRequested += HomeView_ContextRequested;
        _systemView.RestartVoiceRequested += async (_, _) => await RestartWakeWordAsync();
        _systemView.DiagnosticsRequested += (_, _) => ShowDiagnostics();
        _assistantView.ConfigureHistory(
            _preferences.SaveConversationHistory,
            _preferences.RecentConversationMessageLimit);

        if (_preferences.SaveConversationHistory)
        {
            _assistantView.LoadConversation(_conversationStore.Load());
        }

        WireSettingsEvents();
        _settingsView.ApplyPreferences(_preferences);
        UpdateAiProviderStatus();
        ApplyPreferences();
        _wakeWordService.Sensitivity = _preferences.WakeWordSensitivity;
        _assistantView.SetVisionAvailability(_preferences.VisionEnabled);
        ConfigureVoiceInputDevices();
        NavigateTo(ShellNavigationPolicy.DefaultDestination, animate: false);
        SetSideRailExpanded(_preferences.SideRailExpanded, animate: false, persist: false);
        UpdateResourceModeIndicator(ResourceGovernorDecision.Normal);
        RefreshRuntimeDashboard();

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

        _taskReminderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(15)
        };
        _taskReminderTimer.Tick += (_, _) => CheckTaskReminders();

        _focusTickTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _focusTickTimer.Tick += (_, _) => CheckFocusTimer();

        _visualContextExpiryTimer.Interval = TimeSpan.FromMinutes(2);
        _visualContextExpiryTimer.Tick += (_, _) =>
        {
            _visualContextExpiryTimer.Stop();
            ClearPendingVisionAttachment();
        };
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

        _settingsView.VoiceResponsesChanged += enabled =>
        {
            _preferences.SpeakVoiceResponses = enabled;
            if (!enabled)
            {
                _voiceOutputService.Stop();
            }

            SavePreferences();
        };

        _settingsView.VoiceInputDeviceChanged += deviceNumber =>
        {
            _ = ChangeVoiceInputDeviceAsync(deviceNumber);
        };

        _settingsView.WakeWordEnabledChanged += enabled =>
        {
            _preferences.WakeWordEnabled = enabled;
            SavePreferences();
            _ = ApplyWakeWordPreferenceAsync(showCapsule: true);
        };

        _settingsView.WakeWordPhraseChanged += phrase =>
        {
            _preferences.WakeWordPhrase = phrase;
            SavePreferences();
            if (_preferences.WakeWordEnabled)
            {
                _ = ApplyWakeWordPreferenceAsync(showCapsule: false);
            }
        };

        _settingsView.WakeWordSensitivityChanged += sensitivity =>
        {
            _preferences.WakeWordSensitivity = sensitivity;
            _wakeWordService.Sensitivity = sensitivity;
            SavePreferences();
            if (_preferences.WakeWordEnabled)
            {
                _ = ApplyWakeWordPreferenceAsync(showCapsule: false);
            }
        };

        _settingsView.WakeWordTestRequested += async (_, _) =>
            await StartWakeWordTestAsync();

        _settingsView.WakeWordAliasFromLastRequested += async (_, _) =>
            await AddLastWakeWordObservationAsAliasAsync();

        _settingsView.WakeWordAliasesClearRequested += async (_, _) =>
            await ClearWakeWordAliasesAsync();

        _settingsView.AiProviderChanged += provider =>
        {
            var preset = AiProviderDefaults.Get(provider);
            _preferences.AiProvider = provider;
            _preferences.AiBaseUrl = preset.BaseUrl;
            _preferences.AiModel = preset.DefaultModel;
            _preferences.AiApiKeyEnvironmentVariable = preset.ApiKeyEnvironmentVariable;
            UpdateAiProviderStatus();
            SavePreferences();
            ConfigureManagedOllamaSupervisor();
        };

        _settingsView.AiBaseUrlChanged += baseUrl =>
        {
            _preferences.AiBaseUrl = AiProviderDefaults.NormalizeBaseUrl(baseUrl);
            SavePreferences();
            ConfigureManagedOllamaSupervisor();
        };

        _settingsView.AiModelChanged += model =>
        {
            _preferences.AiModel = model.Trim();
            UpdateAiProviderStatus();
            SavePreferences();
        };

        _settingsView.AiApiKeyEnvironmentVariableChanged += variableName =>
        {
            _preferences.AiApiKeyEnvironmentVariable = variableName.Trim();
            SavePreferences();
        };

        _settingsView.ShareSystemMetricsWithAiChanged += enabled =>
        {
            _preferences.ShareSystemMetricsWithAi = enabled;
            SavePreferences();
        };

        _settingsView.VisionEnabledChanged += enabled =>
        {
            _preferences.VisionEnabled = enabled;
            _assistantView.SetVisionAvailability(enabled);
            if (!enabled)
            {
                ClearPendingVisionAttachment();
            }
            SavePreferences();
            RefreshRuntimeDashboard();
        };

        _settingsView.ResourceGovernorEnabledChanged += enabled =>
        {
            _preferences.ResourceGovernorEnabled = enabled;
            SavePreferences();
            _ = RefreshMetricsAsync();
        };

        _settingsView.PauseWakeWordInGameModeChanged += enabled =>
        {
            _preferences.PauseWakeWordInGameMode = enabled;
            SavePreferences();
            _ = ApplyResourceGovernorDecisionAsync(_resourceDecision);
        };

        _settingsView.ProtectVisionWhenBusyChanged += enabled =>
        {
            _preferences.ProtectVisionWhenBusy = enabled;
            SavePreferences();
        };

        _settingsView.StartWithWindowsChanged += enabled =>
        {
            var result = _startupService.SetEnabled(enabled);
            if (result.Success)
            {
                _preferences.StartWithWindows = enabled;
                SavePreferences();
            }
            else
            {
                _settingsView.SetStartWithWindows(_preferences.StartWithWindows);
            }

            _settingsView.SetWindowsIntegrationStatus(result.Message, result.Success);
        };

        _settingsView.MinimizeToTrayChanged += enabled =>
        {
            _preferences.MinimizeToTray = enabled;
            SavePreferences();
            _settingsView.SetWindowsIntegrationStatus(
                enabled
                    ? "Cerrar Kohana lo ocultará en la bandeja."
                    : "Cerrar Kohana terminará completamente la aplicación.",
                isSuccess: null);
        };

        _settingsView.WindowsNotificationsChanged += enabled =>
        {
            _preferences.ShowWindowsNotifications = enabled;
            SavePreferences();
        };

        _settingsView.NotificationSoundsChanged += enabled =>
        {
            _preferences.PlayNotificationSounds = enabled;
            SavePreferences();
        };

        _settingsView.AiTestConnectionRequested += async (_, _) =>
            await TestAiConnectionAsync();

        _settingsView.ManageModelsRequested += (_, _) =>
            ShowModelManager();

        _settingsView.DiagnosticsRequested += (_, _) =>
            ShowDiagnostics();

        _settingsView.OnboardingRequested += async (_, _) =>
            await ShowOnboardingAsync();
    }

    private void ShowModelManager()
    {
        var baseUrl = _preferences.AiProvider == AiProviderKind.Ollama
            ? _preferences.AiBaseUrl
            : AiProviderDefaults.Get(AiProviderKind.Ollama).BaseUrl;
        var window = new ModelManagerWindow(baseUrl, _preferences.AiModel)
        {
            Owner = this
        };

        if (window.ShowDialog() != true ||
            string.IsNullOrWhiteSpace(window.SelectedModel))
        {
            return;
        }

        _preferences.AiProvider = AiProviderKind.Ollama;
        _preferences.AiBaseUrl = AiProviderDefaults.Get(AiProviderKind.Ollama).BaseUrl;
        _preferences.AiModel = window.SelectedModel;
        _preferences.AiApiKeyEnvironmentVariable = string.Empty;
        _settingsView.ApplyPreferences(_preferences);
        UpdateAiProviderStatus();
        SavePreferences();
        _capsuleWindow.ShowMessage(
            CapsuleKind.Success,
            "Modelo seleccionado",
            window.SelectedModel,
            _preferences.Position);
    }

    private void ShowDiagnostics()
    {
        var window = new DiagnosticsWindow(
            _preferences,
            _voiceInputService.GetInputDevices(),
            _voiceInputService.IsReady,
            _wakeWordService.IsReady,
            _wakeWordService.IsListening,
            trayActive: true,
            _startupService.IsEnabled())
        {
            Owner = this
        };
        window.ShowDialog();
    }

    private async Task ShowOnboardingAsync()
    {
        await PauseWakeWordAsync();
        _voiceOutputService.Stop();

        var window = new OnboardingWindow(_preferences, _settingsStore)
        {
            Owner = this
        };
        window.ShowDialog();

        _preferences.StartWithWindows = _startupService.IsEnabled();
        _settingsView.ApplyPreferences(_preferences);
        ApplyPreferences();
        UpdateAiProviderStatus();
        ConfigureManagedOllamaSupervisor();
        _assistantView.SetVisionAvailability(_preferences.VisionEnabled);
        ConfigureVoiceInputDevices();
        await ApplyWakeWordPreferenceAsync(showCapsule: false);
    }

    private void SideRailToggleButton_Click(object sender, RoutedEventArgs e)
    {
        SetSideRailExpanded(!_sideRailExpanded, animate: true, persist: true);
    }

    private void SetSideRailExpanded(bool expanded, bool animate, bool persist = true)
    {
        _sideRailExpanded = expanded;
        if (persist)
        {
            _preferences.SideRailExpanded = expanded;
            SavePreferences();
        }
        SideRailToggleButton.ToolTip = expanded
            ? "Contraer navegación"
            : "Expandir navegación";
        ApplySideRailButtonLayout(expanded);

        var targetWidth = expanded ? 194d : 68d;

        if (!animate || !_preferences.AnimationsEnabled)
        {
            SideRailBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
            SideRailBorder.Width = targetWidth;
            return;
        }

        var currentWidth = SideRailBorder.ActualWidth > 0
            ? SideRailBorder.ActualWidth
            : SideRailBorder.Width;
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var animation = new DoubleAnimation(
            currentWidth,
            targetWidth,
            TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = easing
        };
        animation.Completed += (_, _) =>
        {
            SideRailBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
            SideRailBorder.Width = targetWidth;
        };
        SideRailBorder.BeginAnimation(FrameworkElement.WidthProperty, animation);
    }

    private void ApplySideRailButtonLayout(bool expanded)
    {
        var buttonWidth = expanded ? 178d : 52d;
        SideRailContentGrid.Width = buttonWidth;
        SideRailToggleButton.Width = buttonWidth;
        SettingsNavButton.Width = buttonWidth;
        SideRailBrandText.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        SideRailChevronRotate.Angle = expanded ? 180 : 0;

        foreach (var label in new[]
                 {
                     HomeNavLabel,
                     AssistantNavLabel,
                     TasksNavLabel,
                     FocusNavLabel,
                     RoutinesNavLabel,
                     AudioNavLabel,
                     CaptureNavLabel,
                     SystemNavLabel,
                     SettingsNavLabel
                 })
        {
            label.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        }

        foreach (var button in new[]
                 {
                     HomeNavButton,
                     AssistantNavButton,
                     TasksNavButton,
                     FocusNavButton,
                     RoutinesNavButton,
                     AudioNavButton,
                     CaptureNavButton,
                     SystemNavButton
                 })
        {
            button.Width = buttonWidth;
        }
    }

    private void CommandPaletteButton_Click(object sender, RoutedEventArgs e)
    {
        ShowCommandPalette();
    }

    private void ShowCommandPalette()
    {
        if (_isClosed)
        {
            return;
        }

        RememberForegroundWindow();
        _commandPaletteWindow.ShowPalette(_preferences.AnimationsEnabled);
    }

    private async void CommandPaletteWindow_PromptSubmitted(
        object? sender,
        CommandPalettePromptEventArgs e)
    {
        _promptFromCommandPalette = true;
        try
        {
            await ProcessPromptAsync(e.Prompt, fromVoice: false);
        }
        finally
        {
            _promptFromCommandPalette = false;
        }
    }

    private void CommandPaletteWindow_WorkspaceRequested(object? sender, EventArgs e)
    {
        ShowAnimated();
        NavigateTo("Assistant", animate: true);
    }

    private void HomeView_CommandRequested(object? sender, EventArgs e)
    {
        ShowCommandPalette();
    }

    private void HomeView_TasksRequested(object? sender, EventArgs e)
    {
        NavigateTo("Tasks", animate: true);
    }

    private void HomeView_FocusRequested(object? sender, EventArgs e)
    {
        NavigateTo("Focus", animate: true);
    }

    private async void HomeView_ContextRequested(object? sender, EventArgs e)
    {
        await LookAtForegroundWindowAsync();
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var windowHandle = new WindowInteropHelper(this).Handle;
        _windowSource = HwndSource.FromHwnd(windowHandle);
        _windowSource?.AddHook(WindowMessageHook);

        if (!RegisterHotKey(windowHandle, ShellHotkeyId, ModAlt, VirtualKeyA))
        {
            _assistantView.AddKohanaMessage("Alt + A ya está siendo utilizado por otra aplicación.");
        }

        if (!RegisterHotKey(windowHandle, PeekHotkeyId, ModAlt | ModShift, VirtualKeyA))
        {
            _assistantView.AddKohanaMessage("Alt + Shift + A ya está siendo utilizado por otra aplicación.");
        }

        if (!RegisterHotKey(
                windowHandle,
                CommandPaletteHotkeyId,
                ModControl,
                VirtualKeySpace))
        {
            _assistantView.AddKohanaMessage(
                "Ctrl + Espacio ya está siendo utilizado por otra aplicación.");
        }

        if (!RegisterHotKey(
                windowHandle,
                LookHotkeyId,
                ModControl | ModShift,
                VirtualKeySpace))
        {
            _assistantView.AddKohanaMessage(
                "Ctrl + Shift + Espacio ya está siendo utilizado por otra aplicación.");
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PositionWindow();
        UpdateClock();
        _clockTimer.Start();
        _metricsTimer.Start();
        _taskReminderTimer.Start();
        _focusTickTimer.Start();
        CheckTaskReminders();
        CheckFocusTimer();

        if (_startHidden)
        {
            Hide();
            SetMetricsCadence(isShellVisible: false);
        }
        else
        {
            SetMetricsCadence(isShellVisible: true);
            ShowAnimated();
        }

        ConfigureManagedOllamaSupervisor();
        await RefreshMetricsAsync();
        _ = InitializeVoiceFeaturesAsync();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _isClosed = true;
        _clockTimer.Stop();
        _metricsTimer.Stop();
        _taskReminderTimer.Stop();
        _focusTickTimer.Stop();
        _visualContextExpiryTimer.Stop();
        _peekWindow.HideImmediately();
        _capsuleWindow.HideImmediately();

        if (_preferences.SaveConversationHistory)
        {
            _conversationStore.Save(_assistantView.GetConversationSnapshot());
        }

        _wakeWordTestActive = false;
        _wakeWordTestCancellation?.Cancel();
        _wakeWordTestCancellation?.Dispose();
        _wakeWordTestCancellation = null;
        _lifetimeCancellation.Cancel();
        _capsuleWindow.Close();
        _commandPaletteWindow.Close();
        _wakeWordService.WakeWordDetected -= WakeWordService_WakeWordDetected;
        _wakeWordService.RecognitionObserved -= WakeWordService_RecognitionObserved;
        _wakeWordService.Dispose();
        if (_aiChatService is IDisposable disposableAiService)
        {
            disposableAiService.Dispose();
        }
        _voiceOutputService.Dispose();
        _voiceInputService.Dispose();
        _trayIcon.Dispose();
        _lifetimeCancellation.Dispose();

        var windowHandle = new WindowInteropHelper(this).Handle;
        if (windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(windowHandle, ShellHotkeyId);
            UnregisterHotKey(windowHandle, PeekHotkeyId);
            UnregisterHotKey(windowHandle, CommandPaletteHotkeyId);
            UnregisterHotKey(windowHandle, LookHotkeyId);
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
        if (message == WmPowerBroadcast)
        {
            var powerEvent = wParam.ToInt32();
            if (powerEvent is PbtApmResumeSuspend or PbtApmResumeAutomatic)
            {
                Dispatcher.BeginInvoke(new Action(HandleSystemResume));
            }

            return IntPtr.Zero;
        }

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
        else if (wParam.ToInt32() == CommandPaletteHotkeyId)
        {
            ShowCommandPalette();
            handled = true;
        }
        else if (wParam.ToInt32() == LookHotkeyId)
        {
            RememberForegroundWindow();
            _ = LookAtForegroundWindowAsync();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void ShowFromBackground()
    {
        if (_isClosed)
        {
            return;
        }

        RememberForegroundWindow();
        ShowAnimated();
    }

    private void ConfigureManagedOllamaSupervisor()
    {
        if (_managedOllamaSupervisor is null || _isClosed)
        {
            return;
        }

        if (!_managedOllamaSupervisor.Configure(_preferences))
        {
            return;
        }

        SetManagedAiRuntimePreparing();
        _managedOllamaSupervisor.StartMonitoring(snapshot =>
            Dispatcher.BeginInvoke(new Action(() =>
                UpdateManagedAiRuntimeState(snapshot))));
    }

    public void SetManagedAiRuntimePreparing()
    {
        if (_isClosed)
        {
            return;
        }

        var model = string.IsNullOrWhiteSpace(_preferences.AiModel)
            ? "modelo local"
            : _preferences.AiModel;
        _assistantView.SetAiProviderStatus(
            $"Ollama · {model} · preparando IA local…");
    }

    public void UpdateManagedAiRuntimeState(OllamaRuntimeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (_isClosed)
        {
            return;
        }

        if (snapshot.IsRunning)
        {
            _runtimeAiStatus = "Ollama listo";
            _runtimeAiHealthy = true;
            RefreshRuntimeDashboard();
            var recovered = _managedAiRuntimeFailureNotified;
            _managedAiRuntimeFailureNotified = false;
            UpdateAiProviderStatus();

            if (recovered)
            {
                _assistantView.AddKohanaMessage(
                    "La IA local volvió a estar disponible automáticamente.");
                _capsuleWindow.ShowMessage(
                    CapsuleKind.Success,
                    "IA local recuperada",
                    "Kohana volvió a iniciar el motor local.",
                    _preferences.Position);
            }

            return;
        }

        _runtimeAiStatus = "Ollama necesita atención";
        _runtimeAiHealthy = false;
        RefreshRuntimeDashboard();
        var model = string.IsNullOrWhiteSpace(_preferences.AiModel)
            ? "modelo local"
            : _preferences.AiModel;
        _assistantView.SetAiProviderStatus(
            $"Ollama · {model} · IA local no disponible");

        if (_managedAiRuntimeFailureNotified)
        {
            return;
        }

        _managedAiRuntimeFailureNotified = true;
        _assistantView.AddKohanaMessage(
            $"No pude preparar la IA local: {snapshot.Message}");
        _capsuleWindow.ShowMessage(
            CapsuleKind.Error,
            "IA local no disponible",
            snapshot.Message,
            _preferences.Position);
    }

    private void HandleSystemResume()
    {
        if (_isClosed)
        {
            return;
        }

        CheckTaskReminders();
        CheckFocusTimer();
        _ = RefreshMetricsAsync();
        _ = EnsureManagedAiRuntimeAfterResumeAsync();
    }

    private async Task EnsureManagedAiRuntimeAfterResumeAsync()
    {
        if (_managedOllamaSupervisor is null || _isClosed)
        {
            return;
        }

        try
        {
            await Task.Delay(
                TimeSpan.FromMilliseconds(800),
                _lifetimeCancellation.Token);
            var snapshot = await _managedOllamaSupervisor.EnsureRunningAsync(
                _lifetimeCancellation.Token);
            UpdateManagedAiRuntimeState(snapshot);
        }
        catch (OperationCanceledException)
        {
            // Nexo se está cerrando.
        }
        catch (Exception exception)
        {
            UpdateManagedAiRuntimeState(new OllamaRuntimeSnapshot(
                OllamaRuntimeState.ManagedInstalled,
                OllamaRuntimeEndpoints.ManagedBaseUrl,
                null,
                exception.Message));
        }
    }

    private void ToggleWindow()
    {
        if (IsVisible && Opacity > 0.1 && !_isHiding)
        {
            HideAnimated();
            return;
        }

        RememberForegroundWindow();
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

        if (_currentDestination.Equals("Home", StringComparison.OrdinalIgnoreCase))
        {
            RefreshHomeView();
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        if (_sideRailExpanded)
        {
            SetSideRailExpanded(expanded: false, animate: true);
        }
        else
        {
            HideAnimated();
        }

        e.Handled = true;
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

    private async void AssistantView_VisionCaptureRequested(object? sender, EventArgs e)
    {
        await LookAtForegroundWindowAsync();
    }

    private void AssistantView_VisionAttachmentCleared(object? sender, EventArgs e)
    {
        ClearPendingVisionAttachment();
    }

    private async void CaptureView_CaptureRequested(object? sender, EventArgs e)
    {
        await CaptureForVisionAsync();
    }

    private async void AssistantView_PromptSubmitted(
        object? sender,
        PromptSubmittedEventArgs e)
    {
        await ProcessPromptAsync(e.Prompt, fromVoice: false);
    }

    private async Task ProcessPromptAsync(string prompt, bool fromVoice)
    {
        if (await TryHandlePendingVoiceDecisionAsync(prompt, fromVoice))
        {
            return;
        }

        _voicePromptActive = fromVoice;

        try
        {
            if (_pendingVisionAttachment is null &&
                VisualContextPromptPolicy.ShouldAcquireVisualContext(prompt, fromVoice))
            {
                await PrepareVisualContextAsync(
                    showWorkspace: false,
                    showFeedback: false,
                    silentContext: true);
            }

            _assistantView.AddUserMessage(prompt);

            // Las rutas locales ya muestran su propio resultado. Evitamos una
            // cápsula genérica que parpadee antes de acciones instantáneas.
            await Task.Yield();

            // La precedencia entre subsistemas vive en `PromptDispatchPolicy`, no aquí.
            // Se evalúan los cuatro parsers y la política decide, de modo que "inicia" deje
            // de significar automáticamente "rutina". Ver defecto D1 de la fase 1.1.
            var routineCommand = _routineCommandParser.Parse(prompt);
            var focusCommand = _focusCommandParser.Parse(prompt);
            var taskCommand = _taskCommandParser.Parse(prompt, DateTimeOffset.Now);
            var interpretation = _commandParser.Parse(prompt);

            var dispatch = PromptDispatchPolicy.Resolve(
                routineCommand,
                focusCommand,
                taskCommand,
                interpretation,
                name => _routineManager.FindBestMatch(name) is not null);

            switch (dispatch.Target)
            {
                case PromptDispatchTarget.Routine:
                    await ExecuteRoutineCommandAsync(routineCommand);
                    return;

                case PromptDispatchTarget.Focus:
                    await ExecuteFocusCommandAsync(focusCommand);
                    return;

                case PromptDispatchTarget.Task:
                    await ExecuteTaskCommandAsync(taskCommand);
                    return;

                case PromptDispatchTarget.LocalCommand:
                    await ExecuteLocalCommandAsync(interpretation.Intent!);
                    return;

                default:
                    await SendPromptToAiAsync(prompt, fromVoice);
                    return;
            }
        }
        finally
        {
            _voicePromptActive = false;
        }
    }

    private async Task SendPromptToAiAsync(string prompt, bool fromVoice)
    {
        if (_promptFromCommandPalette)
        {
            ShowAnimated();
            NavigateTo("Assistant", animate: true);
        }

        var configuration = BuildAiConfiguration();
        if (!configuration.IsEnabled)
        {
            const string unavailableMessage =
                "La consulta es abierta, pero la IA está desactivada. Puedes elegir OpenAI, Ollama, LM Studio o un servidor compatible en Personalización.";
            _assistantView.AddKohanaMessage(unavailableMessage);
            _capsuleWindow.ShowMessage(
                CapsuleKind.Information,
                "IA desactivada",
                "Elige un proveedor desde Personalización.",
                _preferences.Position);
            SpeakVoiceResult("La inteligencia artificial está desactivada.");
            return;
        }

        var resourceDecision = await EnsureFreshResourceDecisionAsync();
        var usesLocalRuntime = AiExecutionLocationPolicy.UsesLocalRuntime(configuration);
        var aiAllowed = usesLocalRuntime
            ? resourceDecision.AllowLocalAi
            : resourceDecision.AllowRemoteAi;

        if (!aiAllowed)
        {
            PresentResourceRestriction(
                resourceDecision,
                usesLocalRuntime
                    ? "La IA local está pausada para proteger el rendimiento."
                    : "Las consultas de IA están pausadas durante el Modo Juego.",
                fromVoice);
            return;
        }

        if (_managedOllamaSupervisor is not null &&
            OllamaRuntimeEndpoints.IsManagedBaseUrl(configuration.BaseUrl))
        {
            _assistantView.SetAiActivity("preparando IA local…");

            OllamaRuntimeSnapshot runtimeSnapshot;
            try
            {
                runtimeSnapshot = await _managedOllamaSupervisor.EnsureRunningAsync(
                    _lifetimeCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                _assistantView.SetAiActivity(null);
                return;
            }
            catch (Exception exception)
            {
                runtimeSnapshot = new OllamaRuntimeSnapshot(
                    OllamaRuntimeState.ManagedInstalled,
                    OllamaRuntimeEndpoints.ManagedBaseUrl,
                    null,
                    exception.Message);
            }

            UpdateManagedAiRuntimeState(runtimeSnapshot);
            if (!runtimeSnapshot.IsRunning)
            {
                _assistantView.SetAiActivity(null);
                SpeakVoiceResult("La inteligencia artificial local no está disponible.");
                return;
            }
        }

        await _aiGate.WaitAsync(_lifetimeCancellation.Token);
        var streamingStarted = false;

        try
        {
            string? systemContext = null;
            if (_preferences.ShareSystemMetricsWithAi &&
                AiContextPolicy.ShouldIncludeSystemMetrics(prompt))
            {
                var snapshotAge = DateTimeOffset.Now - _latestSnapshot.CapturedAt;
                if (_latestSnapshot.CapturedAt == DateTimeOffset.MinValue ||
                    snapshotAge > TimeSpan.FromSeconds(5))
                {
                    await RefreshMetricsAsync();
                }

                systemContext = BuildAiSystemContext(_latestSnapshot);
            }

            if (_pendingVisionAttachment is not null &&
                !string.IsNullOrWhiteSpace(_visualContextMetadata))
            {
                systemContext = string.IsNullOrWhiteSpace(systemContext)
                    ? _visualContextMetadata
                    : systemContext + Environment.NewLine + _visualContextMetadata;
            }

            var images = _pendingVisionAttachment is { } image
                ? new[] { image }
                : null;
            var requestMode = VisionIntentPolicy.Resolve(
                prompt,
                images is { Length: > 0 });
            var activity = requestMode == AiRequestMode.VisionTechnicalDiagnostic
                ? "leyendo el error…"
                : "pensando…";

            _assistantView.SetAiActivity(activity);
            _assistantView.BeginKohanaStreamingMessage(
                requestMode == AiRequestMode.VisionTechnicalDiagnostic
                    ? "Analizando la evidencia visible…"
                    : "Pensando…");
            streamingStarted = true;

            _capsuleWindow.ShowMessage(
                CapsuleKind.Processing,
                requestMode == AiRequestMode.VisionTechnicalDiagnostic
                    ? "Diagnosticando captura"
                    : $"Consultando {configuration.DisplayName}",
                string.IsNullOrWhiteSpace(configuration.Model)
                    ? "Preparando la solicitud…"
                    : configuration.Model,
                _preferences.Position);

            var request = new AiChatRequest(
                _assistantView.GetConversationSnapshot(),
                NexoAiInstructions.Default,
                systemContext,
                images,
                requestMode);

            var receivedFirstChunk = false;

            await foreach (var chunk in _aiChatService.StreamAsync(
                configuration,
                request,
                _lifetimeCancellation.Token))
            {
                if (!receivedFirstChunk)
                {
                    receivedFirstChunk = true;
                    _assistantView.SetAiActivity("respondiendo…");
                }

                _assistantView.AppendKohanaStreamingText(chunk);
            }

            var finalText = _assistantView.CompleteKohanaStreamingMessage();
            streamingStarted = false;

            if (string.IsNullOrWhiteSpace(finalText))
            {
                throw new AiChatStreamException(
                    "El proveedor terminó la respuesta sin enviar texto utilizable.");
            }

            _capsuleWindow.ShowMessage(
                CapsuleKind.Success,
                "Respuesta lista",
                SummarizeForCapsule(finalText),
                _preferences.Position);

            if (_visualContextPersistent)
            {
                RestartVisualContextExpiry();
            }
            else
            {
                ClearPendingVisionAttachment();
            }

            if (fromVoice)
            {
                SpeakVoiceResult(finalText);
            }
        }
        catch (AiChatStreamException exception)
        {
            if (streamingStarted)
            {
                _assistantView.CancelKohanaStreamingMessage();
            }

            _assistantView.AddKohanaMessage(
                $"No pude obtener una respuesta: {exception.Message}");
            _capsuleWindow.ShowMessage(
                CapsuleKind.Error,
                "La IA no respondió",
                exception.Message,
                _preferences.Position);
            SpeakVoiceResult("No pude obtener una respuesta de la inteligencia artificial.");
        }
        catch (OperationCanceledException)
        {
            if (streamingStarted)
            {
                _assistantView.CancelKohanaStreamingMessage();
            }

            if (!_isClosed)
            {
                _assistantView.AddKohanaMessage("La consulta fue cancelada.");
            }
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException or System.Text.Json.JsonException)
        {
            if (streamingStarted)
            {
                _assistantView.CancelKohanaStreamingMessage();
            }

            const string detail =
                "La conexión se interrumpió mientras Kohana recibía la respuesta.";
            _assistantView.AddKohanaMessage($"No pude obtener una respuesta: {detail}");
            _capsuleWindow.ShowMessage(
                CapsuleKind.Error,
                "Respuesta interrumpida",
                detail,
                _preferences.Position);
        }
        finally
        {
            _assistantView.SetAiActivity(null);
            _aiGate.Release();
        }
    }

    private Task LookAtForegroundWindowAsync() =>
        PrepareVisualContextAsync(
            showWorkspace: true,
            showFeedback: true,
            silentContext: false);

    private async Task<bool> PrepareVisualContextAsync(
        bool showWorkspace,
        bool showFeedback,
        bool silentContext)
    {
        if (!_preferences.VisionEnabled)
        {
            if (showFeedback)
            {
                _capsuleWindow.ShowMessage(
                    CapsuleKind.Warning,
                    "Kohana Vision desactivado",
                    "Actívalo desde Personalización.",
                    _preferences.Position,
                    force: true);
            }

            return false;
        }

        var resourceDecision = await EnsureFreshResourceDecisionAsync();
        if (_preferences.ProtectVisionWhenBusy && !resourceDecision.AllowVision)
        {
            PresentResourceRestriction(
                resourceDecision,
                "Mirar está pausado para evitar tirones o pérdida de rendimiento.",
                fromVoice: silentContext);
            return false;
        }

        RememberForegroundWindow();

        var ownHandle = new WindowInteropHelper(this).Handle;
        var targets = _screenCaptureService.GetAvailableTargets(ownHandle.ToInt64());
        var target = targets.FirstOrDefault(candidate =>
            candidate.Kind == VisionCaptureKind.Window &&
            candidate.NativeHandle == _lastExternalWindowHandle);

        if (target is null)
        {
            if (showFeedback)
            {
                _capsuleWindow.ShowMessage(
                    CapsuleKind.Information,
                    "No encontré qué mirar",
                    "Activa una ventana y vuelve a intentarlo.",
                    _preferences.Position,
                    force: true);
            }

            return false;
        }

        if (showFeedback)
        {
            _capsuleWindow.ShowMessage(
                CapsuleKind.Processing,
                "Mirando la ventana activa",
                target.Title,
                _preferences.Position);
        }

        VisionCaptureResult result;
        try
        {
            result = await _screenCaptureService.CaptureAsync(
                target,
                _lifetimeCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }

        if (!result.IsSuccess || result.PngBytes is null)
        {
            if (showFeedback)
            {
                _capsuleWindow.ShowMessage(
                    CapsuleKind.Error,
                    "No pude mirar esta ventana",
                    result.Detail,
                    _preferences.Position,
                    force: true);
            }

            return false;
        }

        _visualContextPersistent = true;
        _silentVisualContext = silentContext;
        _visualContextMetadata =
            "Contexto visual temporal de Windows.\n" +
            $"Aplicación: {target.Subtitle}\n" +
            $"Ventana: {target.Title}\n" +
            $"Tamaño visible: {result.Width} × {result.Height} píxeles.\n" +
            "La imagen se procesó en memoria y no se guardó en disco.";
        _pendingVisionAttachment = AiImageAttachment.FromBytes(
            result.PngBytes,
            "image/png",
            target.Title);

        if (!silentContext)
        {
            _assistantView.SetVisionAttachment(
                target.Title,
                result.PngBytes,
                isVisualContext: true);
        }

        RestartVisualContextExpiry();

        if (showWorkspace)
        {
            ShowAnimated();
            NavigateTo("Assistant", animate: true);
        }

        if (showFeedback)
        {
            _capsuleWindow.ShowMessage(
                CapsuleKind.Success,
                "Contexto visual listo",
                $"Estoy viendo {target.Title}. Pregunta lo que necesites.",
                _preferences.Position);
        }

        return true;
    }

    private void RestartVisualContextExpiry()
    {
        if (!_visualContextPersistent)
        {
            return;
        }

        _visualContextExpiryTimer.Stop();
        _visualContextExpiryTimer.Start();
    }

    private async Task CaptureForVisionAsync()
    {
        if (!_preferences.VisionEnabled)
        {
            _assistantView.AddKohanaMessage(
                "Kohana Vision está desactivado. Puedes activarlo en Personalización → Inteligencia artificial.");
            _capsuleWindow.ShowMessage(
                CapsuleKind.Warning,
                "Kohana Vision desactivado",
                "Actívalo desde Personalización.",
                _preferences.Position);
            return;
        }

        RememberForegroundWindow();
        ShowAnimated();
        NavigateTo("Assistant", animate: true);

        var ownHandle = new WindowInteropHelper(this).Handle;
        var targets = _screenCaptureService.GetAvailableTargets(ownHandle.ToInt64());
        if (targets.Count == 0)
        {
            _capsuleWindow.ShowMessage(
                CapsuleKind.Warning,
                "No encontré qué capturar",
                "Abre la ventana que quieras analizar e inténtalo de nuevo.",
                _preferences.Position);
            return;
        }

        var picker = new VisionTargetPickerWindow(targets, _lastExternalWindowHandle)
        {
            Owner = this
        };

        if (picker.ShowDialog() != true || picker.SelectedTarget is null)
        {
            return;
        }

        var selectedTarget = picker.SelectedTarget;
        _capsuleWindow.ShowMessage(
            CapsuleKind.Processing,
            "Preparando captura",
            selectedTarget.Title,
            _preferences.Position);

        VisionCaptureResult result;
        try
        {
            Hide();
            await Task.Delay(180, _lifetimeCancellation.Token);
            result = await _screenCaptureService.CaptureAsync(
                selectedTarget,
                _lifetimeCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (!_isClosed)
            {
                ShowAnimated();
            }
        }

        if (!result.IsSuccess || result.PngBytes is null)
        {
            _capsuleWindow.ShowMessage(
                CapsuleKind.Error,
                "No pude capturar",
                result.Detail,
                _preferences.Position);
            return;
        }

        var preview = new VisionPreviewWindow(result.Title, result.PngBytes)
        {
            Owner = this
        };

        if (preview.ShowDialog() != true)
        {
            _capsuleWindow.ShowMessage(
                CapsuleKind.Information,
                "Captura descartada",
                "La imagen no se compartió ni se guardó.",
                _preferences.Position);
            return;
        }

        _visualContextExpiryTimer.Stop();
        _visualContextPersistent = false;
        _silentVisualContext = false;
        _visualContextMetadata = null;
        _pendingVisionAttachment = AiImageAttachment.FromBytes(
            result.PngBytes,
            "image/png",
            result.Title);
        _assistantView.SetVisionAttachment(result.Title, result.PngBytes);
        NavigateTo("Assistant", animate: true);
        _capsuleWindow.ShowMessage(
            CapsuleKind.Success,
            "Captura lista",
            "Escribe o di qué quieres saber sobre la imagen.",
            _preferences.Position);
    }

    private void ClearPendingVisionAttachment()
    {
        _visualContextExpiryTimer.Stop();
        _visualContextPersistent = false;
        _silentVisualContext = false;
        _visualContextMetadata = null;
        _pendingVisionAttachment = null;
        _assistantView.ClearVisionAttachment();
    }

    private void RememberForegroundWindow()
    {
        var foreground = GetForegroundWindow();
        var ownHandle = new WindowInteropHelper(this).Handle;
        var paletteHandle = new WindowInteropHelper(_commandPaletteWindow).Handle;

        if (foreground != IntPtr.Zero &&
            foreground != ownHandle &&
            foreground != paletteHandle)
        {
            _lastExternalWindowHandle = foreground.ToInt64();
        }
    }

    private async Task TestAiConnectionAsync()
    {
        var configuration = BuildAiConfiguration();
        _settingsView.SetAiTestInProgress(true);
        _settingsView.SetAiConnectionStatus(
            $"Probando {configuration.DisplayName}…",
            isSuccess: null);

        try
        {
            var result = await _aiChatService.TestConnectionAsync(
                configuration,
                _lifetimeCancellation.Token);

            var detail = result.Detail;
            if (result.IsSuccess && result.Models.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(_preferences.AiModel) &&
                    result.Models.Count == 1)
                {
                    _preferences.AiModel = result.Models[0];
                    _settingsView.SetAiModel(result.Models[0]);
                    SavePreferences();
                    detail += $" Modelo seleccionado: {result.Models[0]}.";
                }
                else
                {
                    var preview = string.Join(", ", result.Models.Take(4));
                    detail += $" Modelos: {preview}";
                    if (result.Models.Count > 4)
                    {
                        detail += "…";
                    }
                }
            }

            _settingsView.SetAiConnectionStatus(detail, result.IsSuccess);
            _capsuleWindow.ShowMessage(
                result.IsSuccess ? CapsuleKind.Success : CapsuleKind.Error,
                result.IsSuccess ? "Proveedor conectado" : "No pude conectar",
                detail,
                _preferences.Position);
        }
        catch (OperationCanceledException)
        {
            _settingsView.SetAiConnectionStatus(
                "La prueba fue cancelada.",
                isSuccess: false);
        }
        finally
        {
            _settingsView.SetAiTestInProgress(false);
        }
    }

    private void UpdateAiProviderStatus()
    {
        if (_preferences.AiProvider == AiProviderKind.Disabled)
        {
            _runtimeAiStatus = "Desactivada";
            _runtimeAiHealthy = false;
            _assistantView.SetAiProviderStatus(
                "IA desactivada · los comandos locales siguen disponibles");
            RefreshRuntimeDashboard();
            return;
        }

        var providerName = AiProviderDefaults.Get(_preferences.AiProvider).DisplayName;
        var model = string.IsNullOrWhiteSpace(_preferences.AiModel)
            ? "sin modelo seleccionado"
            : _preferences.AiModel;
        _runtimeAiStatus = _preferences.AiProvider == AiProviderKind.Ollama
            ? "Ollama configurado"
            : $"{providerName} configurado";
        _runtimeAiHealthy = true;
        _assistantView.SetAiProviderStatus($"{providerName} · {model}");
        RefreshRuntimeDashboard();
    }

    private AiProviderConfiguration BuildAiConfiguration()
    {
        return new AiProviderConfiguration(
            _preferences.AiProvider,
            _preferences.AiBaseUrl,
            _preferences.AiModel,
            _preferences.AiApiKeyEnvironmentVariable);
    }

    private static string BuildAiSystemContext(SystemSnapshot snapshot)
    {
        var topProcess = string.IsNullOrWhiteSpace(snapshot.TopProcessName)
            ? "no disponible"
            : $"{snapshot.TopProcessName} ({snapshot.TopProcessWorkingSetBytes.GetValueOrDefault() / 1024d / 1024d:0} MB)";

        return
            $"Captura: {snapshot.CapturedAt:O}\n" +
            $"CPU: {FormatPercentage(snapshot.CpuUsagePercent)}\n" +
            $"RAM: {FormatPercentage(snapshot.MemoryUsagePercent)}\n" +
            $"GPU: {FormatPercentage(snapshot.GpuUsagePercent)}\n" +
            $"Disco del sistema: {FormatPercentage(snapshot.SystemDriveUsagePercent)}\n" +
            $"Proceso con mayor memoria: {topProcess}";
    }

    private static string SummarizeForCapsule(string text)
    {
        var compact = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return compact.Length <= 120
            ? compact
            : compact[..120] + "…";
    }

    private void ConfigureVoiceInputDevices()
    {
        var devices = _voiceCoordinator.GetInputDevices();
        var selectedDeviceNumber = devices.Any(device =>
            device.DeviceNumber == _preferences.VoiceInputDeviceNumber)
            ? _preferences.VoiceInputDeviceNumber
            : devices.FirstOrDefault()?.DeviceNumber ?? -1;

        _preferences.VoiceInputDeviceNumber = selectedDeviceNumber;

        // VoiceCoordinator.InputDeviceNumber aplica el valor a _voiceInputService y a
        // _wakeWordService en un único setter: efecto idéntico a las dos asignaciones
        // directas que sustituye (confirmado leyendo VoiceCoordinator.cs antes de este
        // cambio), en el mismo orden (entrada de voz primero, wake word después).
        _voiceCoordinator.InputDeviceNumber = selectedDeviceNumber;
        _settingsView.SetVoiceInputDevices(devices, selectedDeviceNumber);
        SavePreferences();
    }

    private async Task ChangeVoiceInputDeviceAsync(int deviceNumber)
    {
        await _voiceGate.WaitAsync();
        try
        {
            await PauseWakeWordAsync();

            // CancelAsync() se deja sin migrar deliberadamente: VoiceCoordinator solo
            // expone CancelPushToTalkAsync, que añade su propio candado de voz interno
            // (no presente hoy en esta ruta) — no es una equivalencia exacta, así que se
            // conserva la llamada directa al servicio (ver informe de la subfase 1.3B1).
            await _voiceInputService.CancelAsync();

            _preferences.VoiceInputDeviceNumber = deviceNumber;
            _voiceCoordinator.InputDeviceNumber = deviceNumber;
            SavePreferences();

            var selectedName = _voiceCoordinator
                .GetInputDevices()
                .FirstOrDefault(device => device.DeviceNumber == deviceNumber)
                ?.Name ?? "micrófono seleccionado";

            _assistantView.SetVoiceAvailability(
                _voiceCoordinator.IsVoiceInputReady,
                $"Micrófono activo: {selectedName}");
            _capsuleWindow.ShowMessage(
                CapsuleKind.Success,
                "Micrófono actualizado",
                selectedName,
                _preferences.Position);
        }
        finally
        {
            try
            {
                await ResumeWakeWordIfEnabledAsync();
            }
            finally
            {
                _voiceGate.Release();
            }
        }
    }

    private async Task<bool> TryHandlePendingVoiceDecisionAsync(
        string prompt,
        bool fromVoice)
    {
        if (string.IsNullOrWhiteSpace(_pendingVoicePrompt))
        {
            return false;
        }

        var normalized = SpanishVoiceTranscriptNormalizer.Normalize(prompt);
        if (IsVoiceConfirmation(normalized))
        {
            var confirmedPrompt = _pendingVoicePrompt;
            _pendingVoicePrompt = null;

            _capsuleWindow.ShowMessage(
                CapsuleKind.Success,
                "Orden confirmada",
                confirmedPrompt,
                _preferences.Position);
            await ProcessPromptAsync(confirmedPrompt, fromVoice);
            return true;
        }

        if (IsVoiceCancellation(normalized))
        {
            _pendingVoicePrompt = null;
            _assistantView.AddUserMessage(prompt);
            _assistantView.AddKohanaMessage("Orden cancelada. No hice ningún cambio.");
            _capsuleWindow.ShowMessage(
                CapsuleKind.Information,
                "Orden cancelada",
                "No se ejecutó ninguna acción.",
                _preferences.Position);
            return true;
        }

        // Una orden nueva reemplaza la transcripción dudosa anterior.
        _pendingVoicePrompt = null;
        return false;
    }

    private static bool IsVoiceConfirmation(string text) =>
        text is "si" or "confirmar" or "confirma" or "correcto" or "adelante";

    private static bool IsVoiceCancellation(string text) =>
        text is "no" or "cancela" or "cancelar" or "olvidalo";

    private async Task PrepareVoiceAsync()
    {
        var requiresDownload = !_voiceCoordinator.IsVoiceInputReady;
        _assistantView.SetVoiceAvailability(
            available: false,
            "Preparando Whisper local…");

        if (requiresDownload && !_isClosed)
        {
            _capsuleWindow.ShowMessage(
                CapsuleKind.Information,
                "Preparando voz local",
                "La primera vez Kohana descarga un modelo multilingüe.",
                _preferences.Position);
        }

        var progress = new Progress<VoicePreparationProgress>(update =>
        {
            _assistantView.SetVoiceAvailability(
                available: false,
                update.Detail);
        });

        try
        {
            var result = await _voiceCoordinator.PrepareVoiceInputAsync(
                progress,
                _lifetimeCancellation.Token);

            _assistantView.SetVoiceAvailability(result.IsReady, result.Detail);
            if (result.IsReady && requiresDownload && !_isClosed)
            {
                _capsuleWindow.ShowMessage(
                    CapsuleKind.Success,
                    "Voz local lista",
                    "Whisper ya puede transcribir órdenes en español.",
                    _preferences.Position);
            }
            else if (!result.IsReady && !_isClosed)
            {
                _capsuleWindow.ShowMessage(
                    CapsuleKind.Error,
                    "Voz local no disponible",
                    result.Detail,
                    _preferences.Position);
            }
        }
        catch (OperationCanceledException)
        {
            // Nexo se está cerrando.
        }
    }

    private async Task InitializeVoiceFeaturesAsync()
    {
        await PrepareVoiceAsync();
        if (_preferences.WakeWordEnabled && !_isClosed)
        {
            await ApplyWakeWordPreferenceAsync(showCapsule: false);
        }
    }

    private async void AssistantView_VoiceInputStarted(object? sender, EventArgs e)
    {
        await _voiceGate.WaitAsync();
        var listeningStarted = false;

        try
        {
            await PauseWakeWordAsync();
            _voiceOutputService.Stop();

            if (!_voiceInputService.IsReady)
            {
                await PrepareVoiceAsync();
                if (!_voiceInputService.IsReady)
                {
                    return;
                }
            }

            var result = await _voiceInputService.StartListeningAsync();

            if (!result.IsAvailable)
            {
                _assistantView.SetVoiceState(AssistantVoiceState.Error, result.Detail);
                _capsuleWindow.ShowMessage(
                    CapsuleKind.Error,
                    "Micrófono no disponible",
                    result.Detail,
                    _preferences.Position);
                return;
            }

            listeningStarted = true;
            _assistantView.SetVoiceState(
                AssistantVoiceState.Listening,
                "Escuchando… suelta Mic cuando termines.");
            _capsuleWindow.ShowMessage(
                CapsuleKind.Processing,
                "Escuchando",
                "Suelta Mic cuando termines de hablar.",
                _preferences.Position);
        }
        finally
        {
            if (!listeningStarted)
            {
                await ResumeWakeWordIfEnabledAsync();
            }

            _voiceGate.Release();
        }
    }

    private async void AssistantView_VoiceInputStopped(object? sender, EventArgs e)
    {
        await _voiceGate.WaitAsync();
        try
        {
            if (!_voiceInputService.IsListening)
            {
                return;
            }

            _assistantView.SetVoiceState(
                AssistantVoiceState.Processing,
                "Transcribiendo localmente con Whisper…");

            var result = await _voiceInputService.StopListeningAsync();
            await HandleVoiceRecognitionResultAsync(result);
        }
        catch (OperationCanceledException)
        {
            _assistantView.SetVoiceState(
                AssistantVoiceState.Idle,
                "La escucha fue cancelada.");
        }
        finally
        {
            await ResumeWakeWordIfEnabledAsync();
            _voiceGate.Release();
        }
    }

    private void WakeWordService_WakeWordDetected(
        object? sender,
        WakeWordDetectedEventArgs e)
    {
        _ = Dispatcher.InvokeAsync(() => HandleWakeWordDetectedAsync(e)).Task.Unwrap();
    }

    private void WakeWordService_RecognitionObserved(
        object? sender,
        WakeWordRecognitionObservedEventArgs e)
    {
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            _lastWakeWordObservation = e;
            if (_wakeWordTestActive)
            {
                _settingsView.SetWakeWordObservation(e);
            }
        }));
    }

    private async Task StartWakeWordTestAsync()
    {
        if (!_preferences.WakeWordEnabled)
        {
            _settingsView.SetWakeWordTestStatus(
                "Activa primero la frase de voz.",
                isSuccess: false);
            return;
        }

        _wakeWordTestCancellation?.Cancel();
        _wakeWordTestCancellation?.Dispose();
        _wakeWordTestCancellation = new CancellationTokenSource();
        _wakeWordTestActive = true;
        _lastWakeWordObservation = null;
        _settingsView.ClearWakeWordObservation();

        _settingsView.SetWakeWordTestStatus(
            $"Escuchando durante 12 segundos. Di “{_preferences.WakeWordPhrase.ToSpokenText()}”.",
            isSuccess: null);
        _capsuleWindow.ShowMessage(
            CapsuleKind.Information,
            "Prueba de activación",
            $"Di “{_preferences.WakeWordPhrase.ToSpokenText()}” con voz natural.",
            _preferences.Position);

        if (!_wakeWordService.IsListening)
        {
            await ApplyWakeWordPreferenceAsync(showCapsule: false);
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(12), _wakeWordTestCancellation.Token);
            if (_wakeWordTestActive)
            {
                _wakeWordTestActive = false;
                var detail = _lastWakeWordObservation is null
                    ? "Vosk no produjo texto. Revisa el micrófono o prueba sensibilidad Alta."
                    : $"Lo último que escuchó Vosk fue “{_lastWakeWordObservation.RecognizedText}”. " +
                      _lastWakeWordObservation.Match.Detail;
                _settingsView.SetWakeWordTestStatus(detail, isSuccess: false);
            }
        }
        catch (OperationCanceledException)
        {
            // La prueba terminó al detectar la frase o al iniciar otra prueba.
        }
    }

    private async Task AddLastWakeWordObservationAsAliasAsync()
    {
        var observed = _lastWakeWordObservation?.RecognizedText;
        if (!WakeWordAliasPolicy.TryNormalize(observed, out var alias, out var detail))
        {
            _settingsView.SetWakeWordTestStatus(detail, isSuccess: false);
            return;
        }

        if (_preferences.WakeWordAliases.Contains(alias, StringComparer.Ordinal))
        {
            _settingsView.SetWakeWordTestStatus($"“{alias}” ya está guardado.", isSuccess: true);
            return;
        }

        if (_preferences.WakeWordAliases.Count >= WakeWordAliasPolicy.MaximumAliases)
        {
            _settingsView.SetWakeWordTestStatus(
                $"Puedes guardar hasta {WakeWordAliasPolicy.MaximumAliases} aliases.",
                isSuccess: false);
            return;
        }

        _preferences.WakeWordAliases.Add(alias);
        _preferences.WakeWordAliases = WakeWordAliasPolicy.NormalizeMany(_preferences.WakeWordAliases);
        _wakeWordService.CustomAliases = _preferences.WakeWordAliases;
        SavePreferences();
        _settingsView.SetWakeWordAliases(_preferences.WakeWordAliases);
        _settingsView.SetWakeWordTestStatus($"Alias “{alias}” guardado.", isSuccess: true);
        await ApplyWakeWordPreferenceAsync(showCapsule: false);
    }

    private async Task ClearWakeWordAliasesAsync()
    {
        _preferences.WakeWordAliases.Clear();
        _wakeWordService.CustomAliases = [];
        SavePreferences();
        _settingsView.SetWakeWordAliases(_preferences.WakeWordAliases);
        _settingsView.SetWakeWordTestStatus("Aliases personales eliminados.", isSuccess: true);
        await ApplyWakeWordPreferenceAsync(showCapsule: false);
    }

    private async Task RestartWakeWordAsync()
    {
        if (!_preferences.WakeWordEnabled)
        {
            _capsuleWindow.ShowMessage(
                CapsuleKind.Information,
                "Voz desactivada",
                "Activa una frase desde Personalización → Voz.",
                _preferences.Position);
            return;
        }

        await PauseWakeWordAsync();
        await ApplyWakeWordPreferenceAsync(showCapsule: false);
        _capsuleWindow.ShowMessage(
            _wakeWordService.IsListening ? CapsuleKind.Success : CapsuleKind.Warning,
            _wakeWordService.IsListening ? "Voz reiniciada" : "Voz no disponible",
            _wakeWordService.IsListening
                ? $"Esperando “{_preferences.WakeWordPhrase.ToSpokenText()}”."
                : "Revisa el micrófono y el diagnóstico.",
            _preferences.Position);
    }

    private static string GetWakeWordMatchLabel(WakeWordMatchKind kind) => kind switch
    {
        WakeWordMatchKind.Phonetic => "pronunciación española",
        WakeWordMatchKind.Approximate => "coincidencia aproximada",
        WakeWordMatchKind.CustomAlias => "alias personal",
        WakeWordMatchKind.Legacy => "frase heredada",
        _ => "coincidencia exacta"
    };

    private async Task HandleWakeWordDetectedAsync(WakeWordDetectedEventArgs e)
    {
        if (_isClosed || !_preferences.WakeWordEnabled)
        {
            return;
        }

        if (_wakeWordTestActive)
        {
            _wakeWordTestActive = false;
            _wakeWordTestCancellation?.Cancel();
            _settingsView.SetWakeWordTestStatus(
                $"Detecté “{e.RecognizedText}” como {GetWakeWordMatchLabel(e.MatchKind)}. La frase funciona.",
                isSuccess: true);
            _capsuleWindow.ShowMessage(
                CapsuleKind.Success,
                "Frase detectada",
                e.RecognizedText,
                _preferences.Position);
            await ApplyWakeWordPreferenceAsync(showCapsule: false);
            return;
        }

        RememberForegroundWindow();
        await _voiceGate.WaitAsync();
        try
        {
            await PauseWakeWordAsync();
            _voiceOutputService.Stop();

            if (!_voiceInputService.IsReady)
            {
                await PrepareVoiceAsync();
                if (!_voiceInputService.IsReady)
                {
                    return;
                }
            }

            _assistantView.SetVoiceState(
                AssistantVoiceState.Listening,
                $"{e.Phrase.ToSpokenText()} detectado. Habla con calma; no cortaré las pausas breves.");
            _capsuleWindow.ShowMessage(
                CapsuleKind.Processing,
                "Te escucho",
                "Habla con naturalidad. Terminaré después de 1.5 segundos de silencio.",
                _preferences.Position);

            var result = await _voiceInputService.ListenForUtteranceAsync(
                maximumDuration: TimeSpan.FromSeconds(20),
                trailingSilence: TimeSpan.FromMilliseconds(1_500),
                initialPcmAudio: e.PreRollAudio,
                initialSpeechPcmAudio: e.PostWakeAudio,
                cancellationToken: _lifetimeCancellation.Token);

            _assistantView.SetVoiceState(
                AssistantVoiceState.Processing,
                "Transcribiendo localmente con Whisper…");
            await HandleVoiceRecognitionResultAsync(result);
        }
        catch (OperationCanceledException)
        {
            if (!_isClosed)
            {
                _assistantView.SetVoiceState(
                    AssistantVoiceState.Idle,
                    "La escucha fue cancelada.");
            }
        }
        finally
        {
            await ResumeWakeWordIfEnabledAsync();
            _voiceGate.Release();
        }
    }

    private async Task HandleVoiceRecognitionResultAsync(VoiceRecognitionResult result)
    {
        if (!result.IsRecognized)
        {
            _assistantView.SetVoiceState(AssistantVoiceState.Error, result.Detail);
            _capsuleWindow.ShowMessage(
                CapsuleKind.Warning,
                "No entendí la orden",
                result.Detail,
                _preferences.Position);
            return;
        }

        _assistantView.SetVoiceState(
            AssistantVoiceState.Idle,
            $"Entendí: “{result.Text}”");

        var normalizedDecision =
            SpanishVoiceTranscriptNormalizer.Normalize(result.Text);
        if (!string.IsNullOrWhiteSpace(_pendingVoicePrompt) &&
            (IsVoiceConfirmation(normalizedDecision) ||
             IsVoiceCancellation(normalizedDecision)))
        {
            await ProcessPromptAsync(result.Text, fromVoice: true);
            return;
        }

        if (result.RequiresConfirmation)
        {
            _pendingVoicePrompt = result.Text;
            var question =
                $"Escuché “{result.Text}”, pero no estoy totalmente seguro. " +
                "Di “Kohana, confirmar”, repite la orden o di “Kohana, cancelar”.";

            _assistantView.AddKohanaMessage(question);
            _capsuleWindow.ShowMessage(
                CapsuleKind.Warning,
                "¿Confirmas la orden?",
                result.Text,
                _preferences.Position);
            return;
        }

        _pendingVoicePrompt = null;
        _capsuleWindow.ShowMessage(
            CapsuleKind.Information,
            "Te escuché",
            result.Text,
            _preferences.Position);

        await ProcessPromptAsync(result.Text, fromVoice: true);
    }

    private async Task ApplyWakeWordPreferenceAsync(bool showCapsule)
    {
        await _wakeWordGate.WaitAsync();
        try
        {
            await _wakeWordService.StopListeningAsync();
            SetWakeWordIndicator(active: false);
            RefreshRuntimeDashboard();

            if (!_preferences.WakeWordEnabled || _isClosed || _voiceInputService.IsListening)
            {
                return;
            }

            if (_preferences.PauseWakeWordInGameMode && _resourceDecision.PauseWakeWord)
            {
                SetWakeWordIndicator(active: false);
                _assistantView.SetVoiceAvailability(
                    _voiceInputService.IsReady,
                    "Activación por voz pausada por Modo Juego.");
                return;
            }

            var requiresDownload = !_wakeWordService.IsReady;
            var progress = new Progress<VoicePreparationProgress>(update =>
            {
                WakeWordIndicatorText.Text = "Preparando voz";
                WakeWordIndicator.Visibility = Visibility.Visible;
                _assistantView.SetVoiceAvailability(
                    _voiceInputService.IsReady,
                    update.Detail);
            });

            var preparation = await _wakeWordService.PrepareAsync(
                progress,
                _lifetimeCancellation.Token);

            if (!preparation.IsReady)
            {
                SetWakeWordIndicator(active: false);
                _assistantView.SetVoiceAvailability(
                    _voiceInputService.IsReady,
                    preparation.Detail);
                if (showCapsule && !_isClosed)
                {
                    _capsuleWindow.ShowMessage(
                        CapsuleKind.Error,
                        "Activación no disponible",
                        preparation.Detail,
                        _preferences.Position);
                }
                return;
            }

            if (!_preferences.WakeWordEnabled || _isClosed)
            {
                SetWakeWordIndicator(active: false);
                return;
            }

            _wakeWordService.Sensitivity = _preferences.WakeWordSensitivity;
            _wakeWordService.CustomAliases = _preferences.WakeWordAliases;
            var start = await _wakeWordService.StartListeningAsync(
                _preferences.WakeWordPhrase,
                _lifetimeCancellation.Token);

            if (!start.IsAvailable)
            {
                SetWakeWordIndicator(active: false);
                _assistantView.SetVoiceAvailability(
                    _voiceInputService.IsReady,
                    start.Detail);
                if (showCapsule && !_isClosed)
                {
                    _capsuleWindow.ShowMessage(
                        CapsuleKind.Error,
                        "No pude escuchar Kohana",
                        start.Detail,
                        _preferences.Position);
                }
                return;
            }

            SetWakeWordIndicator(active: true);
            RefreshRuntimeDashboard();
            _assistantView.SetVoiceAvailability(
                _voiceInputService.IsReady,
                $"Di “{_preferences.WakeWordPhrase.ToSpokenText()}” y la orden de corrido, o espera “Te escucho”.");

            if (showCapsule && !_isClosed)
            {
                _capsuleWindow.ShowMessage(
                    CapsuleKind.Success,
                    "Activación por voz lista",
                    $"Puedes decir “{_preferences.WakeWordPhrase.ToSpokenText()}, abre PowerShell” de corrido.",
                    _preferences.Position);
            }
            else if (requiresDownload && !_isClosed)
            {
                _capsuleWindow.ShowMessage(
                    CapsuleKind.Success,
                    "Detector local instalado",
                    "La frase de activación ya funciona sin cuenta ni clave.",
                    _preferences.Position);
            }
        }
        catch (OperationCanceledException)
        {
            // Nexo se está cerrando o la preparación fue cancelada.
        }
        finally
        {
            _wakeWordGate.Release();
        }
    }

    private async Task PauseWakeWordAsync()
    {
        await _wakeWordGate.WaitAsync();
        try
        {
            await _wakeWordService.StopListeningAsync();
            SetWakeWordIndicator(active: false);
        }
        finally
        {
            _wakeWordGate.Release();
        }
    }

    private Task ResumeWakeWordIfEnabledAsync()
    {
        return _preferences.WakeWordEnabled &&
               !_isClosed &&
               !(_preferences.PauseWakeWordInGameMode && _resourceDecision.PauseWakeWord)
            ? ApplyWakeWordPreferenceAsync(showCapsule: false)
            : Task.CompletedTask;
    }

    private void SetWakeWordIndicator(bool active)
    {
        WakeWordIndicator.Visibility = active
            ? Visibility.Visible
            : Visibility.Collapsed;
        WakeWordIndicatorText.Text = active
            ? $"{_preferences.WakeWordPhrase.ToSpokenText()} atento"
            : "Voz pausada";
    }

    private async void RoutinesView_ExecuteRequested(
        object? sender,
        RoutineRequestedEventArgs e)
    {
        var routine = _routineManager.GetAll()
            .FirstOrDefault(candidate => candidate.Id == e.RoutineId);
        if (routine is not null)
        {
            await RunRoutineAsync(routine);
        }
    }

    private async Task ExecuteRoutineCommandAsync(RoutineCommand command)
    {
        switch (command.Type)
        {
            case RoutineCommandType.OpenRoutines:
                ShowAnimated();
                NavigateTo("Routines", animate: true);
                _assistantView.AddKohanaMessage("Abrí el módulo de rutinas.");
                return;

            case RoutineCommandType.ListRoutines:
                var available = _routineManager.GetAll()
                    .Where(routine => routine.IsEnabled)
                    .Select(routine => $"• {routine.Name}: “{routine.TriggerPhrase}”")
                    .ToArray();
                _assistantView.AddKohanaMessage(
                    available.Length == 0
                        ? "No hay rutinas activas."
                        : "Rutinas disponibles:" + Environment.NewLine + string.Join(Environment.NewLine, available));
                _capsuleWindow.ShowMessage(
                    CapsuleKind.Information,
                    "Rutinas disponibles",
                    available.Length == 0 ? "No hay rutinas activas." : $"{available.Length} rutinas activas.",
                    _preferences.Position);
                return;

            case RoutineCommandType.RunRoutine:
                var routine = _routineManager.FindBestMatch(command.RoutineName);
                if (routine is null)
                {
                    _assistantView.AddKohanaMessage(
                        $"No encontré una rutina que coincida con “{command.RoutineName}”.");
                    _capsuleWindow.ShowMessage(
                        CapsuleKind.Warning,
                        "Rutina no encontrada",
                        command.RoutineName,
                        _preferences.Position);
                    return;
                }

                await RunRoutineAsync(routine);
                return;
        }
    }

    private async Task RunRoutineAsync(RoutineDefinition routine)
    {
        if (!routine.IsEnabled)
        {
            _assistantView.AddKohanaMessage($"La rutina {routine.Name} está desactivada.");
            return;
        }

        // La aprobación es por ejecución y se pasa explícitamente al runner. Crear la rutina
        // no concede permiso permanente para ejecutar comandos arbitrarios (defecto D2).
        var approval = RoutineExecutionApproval.NotConfirmed;

        if (AutomationPermissionPolicy.RequiresConfirmation(routine))
        {
            var preview = string.Join(
                Environment.NewLine,
                routine.Steps
                    .Where(step => step.IsEnabled)
                    .Select((step, index) => $"{index + 1}. {DescribeAutomationAction(step)}"));
            var decision = MessageBox.Show(
                $"Kohana ejecutará estas acciones:" + Environment.NewLine + Environment.NewLine + preview,
                $"Ejecutar {routine.Name}",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (decision != MessageBoxResult.Yes)
            {
                _assistantView.AddKohanaMessage($"Cancelé la rutina {routine.Name}.");
                return;
            }

            approval = RoutineExecutionApproval.ConfirmedByUser;
        }

        _capsuleWindow.ShowMessage(
            CapsuleKind.Processing,
            $"Ejecutando {routine.Name}",
            $"{routine.Steps.Count(step => step.IsEnabled)} acciones permitidas",
            _preferences.Position);

        try
        {
            var report = await _routineRunner.RunAsync(
                routine,
                approval,
                _lifetimeCancellation.Token);
            _assistantView.AddKohanaMessage(report.BuildSummary());
            _tasksView.Refresh();
            _focusView.Refresh(DateTimeOffset.Now);
            await _audioView.RefreshAsync(force: true);
            _routinesView.Refresh();

            _capsuleWindow.ShowMessage(
                report.Succeeded ? CapsuleKind.Success : CapsuleKind.Warning,
                report.Succeeded ? "Rutina completada" : "Rutina completada con avisos",
                $"{report.SucceededCount} de {report.Results.Count} acciones listas.",
                _preferences.Position,
                TimeSpan.FromSeconds(8));
            SpeakVoiceResult(
                report.Succeeded
                    ? $"La rutina {routine.Name} terminó correctamente."
                    : $"La rutina {routine.Name} terminó con algunos avisos.");
        }
        catch (OperationCanceledException)
        {
            if (!_isClosed)
            {
                _assistantView.AddKohanaMessage($"La rutina {routine.Name} fue cancelada.");
            }
        }
    }

    private static string DescribeAutomationAction(AutomationAction action) => action.Type switch
    {
        AutomationActionType.OpenApplication => $"Abrir {action.Target}",
        AutomationActionType.OpenFolder => $"Abrir carpeta {action.WorkingDirectory}",
        AutomationActionType.OpenTerminal => $"Abrir PowerShell en {action.WorkingDirectory}",
        AutomationActionType.SetApplicationVolume => $"Poner {action.Target} al {action.NumericValue:0}%",
        AutomationActionType.MuteApplication => $"Silenciar {action.Target}",
        AutomationActionType.UnmuteApplication => $"Activar el sonido de {action.Target}",
        AutomationActionType.StartFocus => $"Iniciar enfoque por {action.NumericValue:0} minutos",
        AutomationActionType.StartBreak => $"Iniciar descanso por {action.NumericValue:0} minutos",
        AutomationActionType.CreateTask => $"Crear tarea: {action.Text}",
        _ => "Acción no permitida"
    };

    private void FocusView_FocusChanged(object? sender, EventArgs e)
    {
        CheckFocusTimer();
        RefreshHomeView();
    }

    private void CheckFocusTimer()
    {
        var now = DateTimeOffset.Now;
        var completion = _focusManager.CollectCompletion(now);
        _focusView.Refresh(now);
        RefreshHomeView();

        if (completion is null)
        {
            return;
        }

        var detail = completion.Kind == FocusSessionKind.Break
            ? "Tu descanso terminó."
            : $"Terminaste {completion.Label.ToLowerInvariant()}.";
        var notificationTitle = completion.Kind == FocusSessionKind.Break
            ? "Descanso terminado"
            : "Sesión completada";
        _capsuleWindow.ShowMessage(
            CapsuleKind.Success,
            notificationTitle,
            detail,
            _preferences.Position,
            TimeSpan.FromSeconds(8));
        _trayIcon.Notify(
            notificationTitle,
            detail,
            TrayNotificationKind.Success,
            _preferences.ShowWindowsNotifications,
            _preferences.PlayNotificationSounds);
        SpeakVoiceResult(detail);
    }

    private Task ExecuteFocusCommandAsync(FocusCommand command)
    {
        FocusOperationResult? operation = null;
        string response;
        CapsuleKind capsuleKind;
        string capsuleTitle;

        switch (command.Type)
        {
            case FocusCommandType.OpenFocus:
                ShowAnimated();
                NavigateTo("Focus", animate: true);
                response = "Abrí el módulo de enfoque.";
                capsuleTitle = "Enfoque abierto";
                capsuleKind = CapsuleKind.Success;
                break;

            case FocusCommandType.Start when command.Duration.HasValue:
                operation = _focusManager.Start(
                    command.Duration.Value,
                    command.Label,
                    command.Kind,
                    DateTimeOffset.Now);
                response = operation.Message;
                capsuleTitle = operation.Success ? "Temporizador iniciado" : "No pude iniciar";
                capsuleKind = operation.Success ? CapsuleKind.Success : CapsuleKind.Warning;
                break;

            case FocusCommandType.Pause:
                operation = _focusManager.Pause(DateTimeOffset.Now);
                response = operation.Message;
                capsuleTitle = operation.Success ? "Temporizador en pausa" : "No pude pausar";
                capsuleKind = operation.Success ? CapsuleKind.Information : CapsuleKind.Warning;
                break;

            case FocusCommandType.Resume:
                operation = _focusManager.Resume(DateTimeOffset.Now);
                response = operation.Message;
                capsuleTitle = operation.Success ? "Temporizador reanudado" : "No pude continuar";
                capsuleKind = operation.Success ? CapsuleKind.Success : CapsuleKind.Warning;
                break;

            case FocusCommandType.Cancel:
                operation = _focusManager.Cancel();
                response = operation.Message;
                capsuleTitle = operation.Success ? "Temporizador cancelado" : "Nada que cancelar";
                capsuleKind = operation.Success ? CapsuleKind.Information : CapsuleKind.Warning;
                break;

            case FocusCommandType.Status:
                response = _focusManager.BuildStatus(DateTimeOffset.Now);
                capsuleTitle = "Estado del temporizador";
                capsuleKind = CapsuleKind.Information;
                break;

            default:
                response = "No pude interpretar esa instrucción de enfoque.";
                capsuleTitle = "Instrucción incompleta";
                capsuleKind = CapsuleKind.Warning;
                break;
        }

        _focusView.Refresh(DateTimeOffset.Now);
        _assistantView.AddKohanaMessage(response);
        _capsuleWindow.ShowMessage(
            capsuleKind,
            capsuleTitle,
            response,
            _preferences.Position);

        if (operation?.Success == true)
        {
            _homeView.AddRecentAction(capsuleTitle, response);
        }

        RefreshHomeView();
        SpeakVoiceResult(response);
        return Task.CompletedTask;
    }

    private void TasksView_TasksChanged(object? sender, EventArgs e)
    {
        CheckTaskReminders();
        RefreshHomeView();
    }

    private void CheckTaskReminders()
    {
        var reminders = _taskManager.CollectDueReminders(DateTimeOffset.Now);
        RefreshHomeView();
        if (reminders.Count == 0)
        {
            return;
        }

        _tasksView.Refresh();
        var first = reminders[0];
        var detail = reminders.Count == 1
            ? first.Title
            : $"{first.Title} y {reminders.Count - 1} más";

        _capsuleWindow.ShowMessage(
            CapsuleKind.Information,
            "Recordatorio",
            detail,
            _preferences.Position,
            TimeSpan.FromSeconds(8));
        _trayIcon.Notify(
            "Recordatorio",
            detail,
            TrayNotificationKind.Information,
            _preferences.ShowWindowsNotifications,
            _preferences.PlayNotificationSounds);
    }

    private Task ExecuteTaskCommandAsync(TaskCommand command)
    {
        string response;
        CapsuleKind capsuleKind;
        string capsuleTitle;

        switch (command.Type)
        {
            case TaskCommandType.OpenTasks:
                ShowAnimated();
                NavigateTo("Tasks", animate: true);
                response = "Abrí tus tareas.";
                capsuleTitle = "Tareas abiertas";
                capsuleKind = CapsuleKind.Success;
                break;

            case TaskCommandType.ListToday:
                response = _taskManager.BuildTodaySummary(DateTimeOffset.Now);
                capsuleTitle = "Pendientes de hoy";
                capsuleKind = CapsuleKind.Information;
                break;

            case TaskCommandType.ListPending:
                response = _taskManager.BuildPendingSummary(DateTimeOffset.Now);
                capsuleTitle = "Tareas pendientes";
                capsuleKind = CapsuleKind.Information;
                break;

            case TaskCommandType.Create when !string.IsNullOrWhiteSpace(command.Title):
            {
                if (command.ReminderEnabled && !command.DueAt.HasValue)
                {
                    response = "Dime cuándo debo recordártelo, por ejemplo: mañana a las 8.";
                    capsuleTitle = "Falta la fecha";
                    capsuleKind = CapsuleKind.Warning;
                    break;
                }

                var task = _taskManager.Create(
                    command.Title,
                    dueAt: command.DueAt,
                    priority: command.Priority,
                    reminderEnabled: command.ReminderEnabled);
                _tasksView.Refresh();

                var schedule = task.DueAt.HasValue
                    ? task.DueAt.Value.ToString("ddd d MMM · HH:mm", new CultureInfo("es-MX"))
                    : "sin fecha";
                response = task.ReminderEnabled
                    ? $"Guardé el recordatorio “{task.Title}” para {schedule}."
                    : $"Agregué “{task.Title}” · {schedule}.";
                capsuleTitle = task.ReminderEnabled ? "Recordatorio guardado" : "Tarea agregada";
                capsuleKind = CapsuleKind.Success;
                break;
            }

            case TaskCommandType.Complete when !string.IsNullOrWhiteSpace(command.Title):
            {
                var result = _taskManager.CompleteMatching(command.Title);
                _tasksView.Refresh();
                response = result.Message;
                capsuleTitle = result.Success ? "Tarea completada" : "Tarea no encontrada";
                capsuleKind = result.Success ? CapsuleKind.Success : CapsuleKind.Warning;
                break;
            }

            case TaskCommandType.Delete when !string.IsNullOrWhiteSpace(command.Title):
            {
                var result = _taskManager.DeleteMatching(command.Title);
                _tasksView.Refresh();
                response = result.Message;
                capsuleTitle = result.Success ? "Tarea eliminada" : "Tarea no encontrada";
                capsuleKind = result.Success ? CapsuleKind.Success : CapsuleKind.Warning;
                break;
            }

            default:
                response = "No pude interpretar esa instrucción de tareas.";
                capsuleTitle = "Instrucción incompleta";
                capsuleKind = CapsuleKind.Warning;
                break;
        }

        _assistantView.AddKohanaMessage(response);
        _capsuleWindow.ShowMessage(
            capsuleKind,
            capsuleTitle,
            response.Replace("\n", " "),
            _preferences.Position);
        SpeakVoiceResult(response);
        return Task.CompletedTask;
    }

    private async Task ExecuteLocalCommandAsync(LocalCommandIntent intent)
    {
        switch (intent.Type)
        {
            case LocalCommandType.ShowPeek:
                if (!_preferences.PeekEnabled)
                {
                    _assistantView.AddKohanaMessage("La vista Peek está desactivada en Personalización.");
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

            case LocalCommandType.ShowCurrentDate:
                ShowCurrentDate();
                break;

            case LocalCommandType.ShowCurrentTime:
                ShowCurrentTime();
                break;

            case LocalCommandType.CaptureForVision:
                await LookAtForegroundWindowAsync();
                break;

            case LocalCommandType.NavigateAssistant:
                ShowShellModule(ShellNavigationPolicy.Assistant, "Asistente abierto");
                break;

            case LocalCommandType.NavigateAudio:
                ShowShellModule(ShellNavigationPolicy.Audio, "Audio abierto");
                break;

            case LocalCommandType.NavigateCapture:
                ShowShellModule(ShellNavigationPolicy.Capture, "Captura abierta");
                break;

            case LocalCommandType.NavigateSystem:
                ShowShellModule(ShellNavigationPolicy.System, "Sistema abierto");
                break;

            case LocalCommandType.NavigateSettings:
                ShowShellModule(ShellNavigationPolicy.Settings, "Ajustes abiertos");
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

            case LocalCommandType.OpenKnownFolder:
                OpenKnownFolder(intent.Target);
                break;

            case LocalCommandType.OpenKnownApplication:
                OpenKnownApplication(intent.Target);
                break;

            case LocalCommandType.SetApplicationVolume:
            case LocalCommandType.ScaleApplicationVolume:
            case LocalCommandType.ChangeApplicationVolume:
            case LocalCommandType.MuteApplication:
            case LocalCommandType.UnmuteApplication:
            case LocalCommandType.LowerAllExcept:
                await ExecuteAudioCommandAsync(intent);
                break;

            default:
                _assistantView.AddKohanaMessage("No pude ejecutar esa orden local todavía.");
                _capsuleWindow.ShowMessage(
                    CapsuleKind.Warning,
                    "Comando no disponible",
                    "La orden fue reconocida, pero aún no tiene una acción conectada.",
                    _preferences.Position);
                break;
        }
    }

    private void ShowCurrentDate()
    {
        var culture = new CultureInfo("es-MX");
        var date = DateTime.Now.ToString(
            "dddd d 'de' MMMM 'de' yyyy",
            culture);
        var response = $"Hoy es {date}.";

        _assistantView.AddKohanaMessage(response);
        _capsuleWindow.ShowMessage(
            CapsuleKind.Success,
            "Fecha actual",
            date,
            _preferences.Position);
        SpeakVoiceResult(response);
    }

    private void ShowCurrentTime()
    {
        var time = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
        var response = $"Son las {time}.";

        _assistantView.AddKohanaMessage(response);
        _capsuleWindow.ShowMessage(
            CapsuleKind.Success,
            "Hora actual",
            time,
            _preferences.Position);
        SpeakVoiceResult(response);
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

        _assistantView.AddKohanaMessage(summary);
        _capsuleWindow.ShowMessage(
            CapsuleKind.Success,
            "Estado del equipo listo",
            $"CPU {FormatPercentage(_latestSnapshot.CpuUsagePercent)} · RAM {FormatPercentage(_latestSnapshot.MemoryUsagePercent)} · GPU {FormatPercentage(_latestSnapshot.GpuUsagePercent)}",
            _preferences.Position);
        SpeakVoiceResult(summary);
    }

    private void ShowShellModule(string destination, string confirmation)
    {
        ShowAnimated();
        NavigateTo(destination, animate: true);
        ShowCommandSuccess(confirmation, "La orden se ejecutó localmente.");
    }

    private void OpenKnownFolder(string? target)
    {
        var (argument, displayName) = target switch
        {
            "downloads" => ("shell:Downloads", "Descargas"),
            "documents" => (QuoteExplorerPath(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)), "Documentos"),
            "pictures" => (QuoteExplorerPath(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)), "Imágenes"),
            "desktop" => (QuoteExplorerPath(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)), "Escritorio"),
            "profile" => (QuoteExplorerPath(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)), "Carpeta personal"),
            _ => (string.Empty, string.Empty)
        };

        if (string.IsNullOrWhiteSpace(argument))
        {
            ShowLocalLaunchFailure("Carpeta no reconocida");
            return;
        }

        if (!TryStartProcess("explorer.exe", argument))
        {
            ShowLocalLaunchFailure(displayName);
            return;
        }

        _assistantView.AddKohanaMessage($"Abrí {displayName}.");
        ShowCommandSuccess($"{displayName} abierto", "La carpeta se abrió localmente.");
    }

    private static string QuoteExplorerPath(string path) =>
        string.IsNullOrWhiteSpace(path) ? string.Empty : $"\"{path}\"";

    private void OpenKnownApplication(string? target)
    {
        var opened = target switch
        {
            "vscode" => TryOpenVisualStudioCode(),
            "calculator" => TryStartProcess("calc.exe"),
            "taskmanager" => TryStartProcess("taskmgr.exe"),
            "explorer" => TryStartProcess("explorer.exe"),
            "windows-settings" => TryStartProcess("ms-settings:"),
            _ => false
        };

        var displayName = target switch
        {
            "vscode" => "Visual Studio Code",
            "calculator" => "Calculadora",
            "taskmanager" => "Administrador de tareas",
            "explorer" => "Explorador de archivos",
            "windows-settings" => "Configuración de Windows",
            _ => "Aplicación"
        };

        if (!opened)
        {
            ShowLocalLaunchFailure(displayName);
            return;
        }

        _assistantView.AddKohanaMessage($"Abrí {displayName}.");
        ShowCommandSuccess($"{displayName} abierto", "La acción se ejecutó localmente.");
    }

    private static bool TryOpenVisualStudioCode()
    {
        var candidates = new[]
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "Microsoft VS Code",
                "Code.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Microsoft VS Code",
                "Code.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft VS Code",
                "Code.exe")
        };

        foreach (var candidate in candidates.Where(File.Exists))
        {
            if (TryStartProcess(candidate))
            {
                return true;
            }
        }

        return TryStartProcess("code");
    }

    private static bool TryStartProcess(string fileName, string arguments = "")
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }

    private void ShowLocalLaunchFailure(string displayName)
    {
        _assistantView.AddKohanaMessage($"No pude abrir {displayName}.");
        _capsuleWindow.ShowMessage(
            CapsuleKind.Error,
            "No se pudo abrir",
            displayName,
            _preferences.Position);
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

            _assistantView.AddKohanaMessage($"{confirmation} en {userFolder}.");
            ShowCommandSuccess(confirmation, userFolder);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _assistantView.AddKohanaMessage($"No pude abrir {fileName}.");
            _capsuleWindow.ShowMessage(
                CapsuleKind.Error,
                "No se pudo abrir",
                fileName,
                _preferences.Position);
        }
    }

    private async Task ExecuteAudioCommandAsync(LocalCommandIntent intent)
    {
        var result = await Task.Run(() => intent.Type switch
        {
            LocalCommandType.SetApplicationVolume =>
                _audioMixerService.SetApplicationVolume(
                    intent.Target ?? string.Empty,
                    intent.Percent.GetValueOrDefault()),

            LocalCommandType.ScaleApplicationVolume =>
                _audioMixerService.ScaleApplicationVolume(
                    intent.Target ?? string.Empty,
                    intent.Factor.GetValueOrDefault(0.5)),

            LocalCommandType.ChangeApplicationVolume =>
                _audioMixerService.ChangeApplicationVolume(
                    intent.Target ?? string.Empty,
                    intent.DeltaPoints.GetValueOrDefault()),

            LocalCommandType.MuteApplication =>
                _audioMixerService.SetApplicationMuted(
                    intent.Target ?? string.Empty,
                    muted: true),

            LocalCommandType.UnmuteApplication =>
                _audioMixerService.SetApplicationMuted(
                    intent.Target ?? string.Empty,
                    muted: false),

            LocalCommandType.LowerAllExcept =>
                _audioMixerService.LowerAllExcept(
                    intent.Target ?? string.Empty,
                    intent.Factor.GetValueOrDefault(0.5)),

            _ => AudioActionResult.Failed("La orden de audio no tiene una acción asociada.")
        });

        PresentAudioResult(result, addToConversation: true);
        await _audioView.RefreshAsync(force: true);
    }

    private void AudioView_ActionCompleted(object? sender, AudioActionEventArgs e)
    {
        PresentAudioResult(e.Result, addToConversation: false);
    }

    private void PresentAudioResult(AudioActionResult result, bool addToConversation)
    {
        if (addToConversation)
        {
            _assistantView.AddKohanaMessage(result.Detail);
        }

        var capsuleKind = result.Status switch
        {
            AudioActionStatus.Success => CapsuleKind.Success,
            AudioActionStatus.NotFound => CapsuleKind.Warning,
            AudioActionStatus.Unavailable => CapsuleKind.Warning,
            _ => CapsuleKind.Error
        };

        _capsuleWindow.ShowMessage(
            capsuleKind,
            result.Title,
            result.Detail,
            _preferences.Position);

        if (result.Status == AudioActionStatus.Success)
        {
            _homeView.AddRecentAction(result.Title, result.Detail);
        }

        SpeakVoiceResult(result.Detail);
    }

    private void ShowCommandSuccess(string title, string detail)
    {
        _capsuleWindow.ShowMessage(
            CapsuleKind.Success,
            title,
            detail,
            _preferences.Position);
        _homeView.AddRecentAction(title, detail);
        RefreshHomeView();
        SpeakVoiceResult(title);
    }

    private void SpeakVoiceResult(string text)
    {
        if (_voicePromptActive && _preferences.SpeakVoiceResponses)
        {
            _voiceOutputService.SpeakShort(text);
        }
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
            if (_sideRailExpanded)
            {
                SetSideRailExpanded(expanded: false, animate: true);
            }
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var destination = ShellNavigationPolicy.ResolveSettingsToggle(
            _currentDestination,
            _previousDestination);

        _previousDestination = ShellNavigationPolicy.ResolvePreviousDestination(
            _currentDestination,
            _previousDestination);

        NavigateTo(destination, animate: true);
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
        UpdateWorkspaceHeader(destination);

        if (destination.Equals("Home", StringComparison.OrdinalIgnoreCase))
        {
            RefreshHomeView();
        }

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
        else if (_currentDestination == "Tasks")
        {
            _tasksView.FocusPrimaryControl();
        }
        else if (_currentDestination == "Focus")
        {
            _focusView.FocusPrimaryControl();
        }
        else if (_currentDestination == "Routines")
        {
            _routinesView.FocusPrimaryControl();
        }
    }

    private void UpdateNavigationState(string destination)
    {
        var buttons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase)
        {
            ["Home"] = HomeNavButton,
            ["Assistant"] = AssistantNavButton,
            ["Tasks"] = TasksNavButton,
            ["Focus"] = FocusNavButton,
            ["Routines"] = RoutinesNavButton,
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
                ? (Brush)FindResource("BrushAccent")
                : (Brush)FindResource("BrushTextSecondary");
        }

        var settingsSelected = destination.Equals("Settings", StringComparison.OrdinalIgnoreCase);
        SettingsNavButton.Background = settingsSelected
            ? (Brush)FindResource("BrushAccentSoft")
            : Brushes.Transparent;
        SettingsNavButton.Foreground = settingsSelected
            ? (Brush)FindResource("BrushAccent")
            : (Brush)FindResource("BrushTextSecondary");
    }

    private void UpdateWorkspaceHeader(string destination)
    {
        var (title, subtitle) = destination switch
        {
            "Home" => ("Inicio", "Una vista breve de lo que importa ahora"),
            "Assistant" => ("Asistente", "Consulta, conversa o comparte contexto"),
            "Tasks" => ("Hoy", "Tareas, prioridades y recordatorios"),
            "Focus" => ("Enfoque", "Sesiones cortas sin perder el ritmo"),
            "Routines" => ("Rutinas", "Acciones repetibles, claras y controladas"),
            "Audio" => ("Audio", "Control local por aplicación"),
            "Capture" => ("Captura", "Selecciona qué puede ver Kohana"),
            "System" => ("Sistema", "Estado y diagnóstico del equipo"),
            "Settings" => ("Personalizar", "Apariencia, privacidad y comportamiento"),
            _ => ("Kohana", "Tu espacio de acciones y contexto")
        };

        WorkspaceTitleText.Text = title;
        WorkspaceSubtitleText.Text = subtitle;
    }

    private void RefreshHomeView()
    {
        var now = DateTimeOffset.Now;
        var localNow = now.LocalDateTime;
        var greeting = localNow.Hour switch
        {
            < 6 => "Buenas noches",
            < 12 => "Buenos días",
            < 19 => "Buenas tardes",
            _ => "Buenas noches"
        };

        var culture = new CultureInfo("es-MX");
        var greetingDetail =
            $"{localNow.ToString("dddd, d 'de' MMMM", culture)} · Kohana está listo";

        var pending = _taskManager.GetAll()
            .Where(task => !task.IsCompleted)
            .OrderBy(task => task.DueAt ?? DateTimeOffset.MaxValue)
            .ThenByDescending(task => task.Priority)
            .ToArray();
        var today = pending
            .Where(task => task.DueAt.HasValue &&
                           task.DueAt.Value.LocalDateTime.Date == localNow.Date)
            .ToArray();
        var overdue = pending.Count(task => task.IsOverdue(now));

        var taskValue = today.Length > 0
            ? today.Length.ToString(CultureInfo.InvariantCulture)
            : overdue > 0
                ? overdue.ToString(CultureInfo.InvariantCulture)
                : "0";
        var taskDetail = today.FirstOrDefault() is { } nextToday
            ? nextToday.DueAt.HasValue
                ? $"Siguiente: {nextToday.Title} · {nextToday.DueAt.Value:HH:mm}"
                : $"Siguiente: {nextToday.Title}"
            : overdue > 0
                ? $"{overdue} vencida{(overdue == 1 ? string.Empty : "s")} necesita atención"
                : pending.FirstOrDefault() is { } nextPending
                    ? $"Próxima: {nextPending.Title}"
                    : "Nada urgente por ahora";

        var focus = _focusManager.GetSnapshot(now);
        string focusValue;
        string focusDetail;
        if (focus.ActiveTimer is { } timer)
        {
            var minutes = Math.Max(0, (int)Math.Ceiling(focus.Remaining.TotalMinutes));
            focusValue = $"{minutes} min";
            focusDetail = timer.Status == FocusTimerStatus.Paused
                ? $"{timer.Label} · en pausa"
                : timer.Label;
        }
        else
        {
            focusValue = "25 min";
            focusDetail = focus.FocusMinutesToday > 0
                ? $"{focus.FocusMinutesToday} min completados hoy"
                : "Listo para empezar";
        }

        var contextTitle = _lastExternalWindowHandle != 0
            ? "Ventana activa recordada"
            : "Lista para analizar";
        var contextDetail = _lastExternalWindowHandle != 0
            ? "Pulsa aquí para capturarla con tu autorización."
            : "Abre una ventana y Kohana podrá verla cuando lo pidas.";

        _homeView.Refresh(new HomeDashboardViewModel(
            greeting,
            greetingDetail,
            taskValue,
            taskDetail,
            focusValue,
            focusDetail,
            contextTitle,
            contextDetail));
    }

    private void ApplyPreferences()
    {
        _preferences.Normalize();
        Width = _preferences.Width;
        ApplyShellOpacity();
        ApplyAccent(_preferences.AccentColor);
        _wakeWordService.Sensitivity = _preferences.WakeWordSensitivity;
        ApplyModuleVisibility();
        _assistantView.SetVisionAvailability(_preferences.VisionEnabled);
    }

    private void ApplyShellOpacity()
    {
        var baseColor = (Color)ColorConverter.ConvertFromString("#0D1119");
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
            Application.Current.Resources["BrushAccentBorder"] = new SolidColorBrush(
                Color.FromArgb(112, accent.R, accent.G, accent.B));
        }
        catch (Exception exception) when (exception is FormatException or NotSupportedException)
        {
            Application.Current.Resources["BrushAccent"] = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#8B6CFF"));
            Application.Current.Resources["BrushAccentSoft"] = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#2D2748"));
            Application.Current.Resources["BrushAccentBorder"] = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#668B6CFF"));
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

        if (ShellNavigationPolicy.TryResolveHiddenModuleFallback(
                module,
                visible,
                _currentDestination,
                out var fallbackDestination))
        {
            NavigateTo(fallbackDestination, animate: true);
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
        // La navegación ahora es vertical. Las preferencias solo cambian
        // la visibilidad de cada acceso, no el número de columnas.
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
            var ownHandle = new WindowInteropHelper(this).Handle.ToInt64();
            var preferredExternalWindow = _lastExternalWindowHandle;
            var snapshot = await Task.Run(_metricsService.ReadSnapshot);
            var decision = _preferences.ResourceGovernorEnabled
                ? await Task.Run(() => _resourceGovernorService.Evaluate(
                    snapshot,
                    preferredExternalWindow,
                    ownHandle))
                : ResourceGovernorDecision.Normal;

            if (_isClosed)
            {
                return;
            }

            _latestSnapshot = snapshot;
            UpdateMetricControls(snapshot);
            await ApplyResourceGovernorDecisionAsync(decision);
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

    private async Task<ResourceGovernorDecision> EnsureFreshResourceDecisionAsync()
    {
        if (!_preferences.ResourceGovernorEnabled)
        {
            return ResourceGovernorDecision.Normal;
        }

        var snapshotAge = DateTimeOffset.Now - _latestSnapshot.CapturedAt;
        if (_latestSnapshot.CapturedAt == DateTimeOffset.MinValue ||
            snapshotAge > TimeSpan.FromSeconds(4))
        {
            await RefreshMetricsAsync();
        }

        return _resourceDecision;
    }

    private async Task ApplyResourceGovernorDecisionAsync(
        ResourceGovernorDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        var previous = _resourceDecision;
        _resourceDecision = decision;
        _capsuleWindow.SuppressTransientMessages = decision.SuppressTransientOverlays;
        if (decision.SuppressTransientOverlays)
        {
            _capsuleWindow.HideImmediately();
        }

        UpdateResourceModeIndicator(decision);

        var decisionChanged =
            previous.Mode != decision.Mode ||
            !previous.Reason.Equals(decision.Reason, StringComparison.Ordinal);

        if (decisionChanged)
        {
            WriteResourceGovernorLog(previous, decision);
            if (previous.Mode != decision.Mode)
            {
                var title = decision.Mode switch
                {
                    ResourceMode.Game => "Modo Juego activo",
                    ResourceMode.Busy => "Rendimiento protegido",
                    _ => "Modo normal restaurado"
                };
                _homeView.AddRecentAction(title, decision.Reason);
            }
        }

        await _resourceGovernorVoiceGate.WaitAsync();
        try
        {
            var shouldPauseWakeWord =
                _preferences.PauseWakeWordInGameMode && decision.PauseWakeWord;

            if (shouldPauseWakeWord)
            {
                if (_wakeWordService.IsListening)
                {
                    await PauseWakeWordAsync();
                }

                _resourceGovernorWakeWordPaused = _preferences.WakeWordEnabled;
                return;
            }

            if (_resourceGovernorWakeWordPaused)
            {
                _resourceGovernorWakeWordPaused = false;
                await ResumeWakeWordIfEnabledAsync();
            }
        }
        finally
        {
            _resourceGovernorVoiceGate.Release();
        }
    }

    private void UpdateResourceModeIndicator(ResourceGovernorDecision decision)
    {
        ResourceModeIndicator.Visibility = decision.Mode == ResourceMode.Normal
            ? Visibility.Collapsed
            : Visibility.Visible;
        ResourceModeText.Text = decision.Mode switch
        {
            ResourceMode.Game => "Modo Juego",
            ResourceMode.Busy => "Equipo ocupado",
            _ => "Normal"
        };
        ResourceModeIndicator.ToolTip = decision.Reason;
        ResourceModeDot.Fill = decision.Mode == ResourceMode.Game
            ? (Brush)FindResource("BrushDanger")
            : (Brush)FindResource("BrushWarning");
        RefreshRuntimeDashboard();
    }

    private void RefreshRuntimeDashboard()
    {
        _systemView.UpdateRuntimeStatus(
            _voiceInputService.IsReady,
            _preferences.WakeWordEnabled,
            _wakeWordService.IsListening,
            _preferences.VisionEnabled,
            _runtimeAiStatus,
            _runtimeAiHealthy,
            _resourceDecision.Mode,
            _resourceDecision.Reason);
    }

    private void PresentResourceRestriction(
        ResourceGovernorDecision decision,
        string detail,
        bool fromVoice)
    {
        var title = decision.Mode == ResourceMode.Game
            ? "Kohana está en Modo Juego"
            : "El equipo está ocupado";
        var message = $"{detail} {decision.Reason}";

        _assistantView.AddKohanaMessage(message);
        _capsuleWindow.ShowMessage(
            CapsuleKind.Warning,
            title,
            detail,
            _preferences.Position,
            force: true);

        if (fromVoice)
        {
            _voiceOutputService.SpeakShort(detail);
        }
    }

    private static void WriteResourceGovernorLog(
        ResourceGovernorDecision previous,
        ResourceGovernorDecision current)
    {
        try
        {
            Directory.CreateDirectory(Nexo.Core.Diagnostics.NexoDataPaths.LogsDirectory);
            File.AppendAllText(
                Nexo.Core.Diagnostics.NexoDataPaths.ResourceGovernorLog,
                $"{DateTimeOffset.Now:O} | {previous.Mode} -> {current.Mode} | {current.Reason}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // El registro no debe afectar el funcionamiento de Nexo.
        }
        catch (UnauthorizedAccessException)
        {
            // El registro no debe afectar el funcionamiento de Nexo.
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
            _assistantView.AddKohanaMessage("La vista Peek está desactivada en Personalización.");
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
            _assistantView.AddKohanaMessage("No se pudo guardar la configuración en este momento.");
        }
        catch (UnauthorizedAccessException)
        {
            _assistantView.AddKohanaMessage("Windows no permitió guardar la configuración.");
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (Nexo.Core.WindowsIntegration.WindowsClosePolicy.ShouldHideInsteadOfClose(
                _preferences.MinimizeToTray,
                _allowExit))
        {
            e.Cancel = true;
            HideToTray(showHint: true);
            return;
        }

        if (!_allowExit)
        {
            e.Cancel = true;
            RequestExit();
        }
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        HideAnimated();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_preferences.MinimizeToTray)
        {
            HideToTray(showHint: true);
            return;
        }

        RequestExit();
    }

    private void HideToTray(bool showHint)
    {
        HideAnimated();

        if (!showHint || _trayHintShown)
        {
            return;
        }

        _trayHintShown = true;
        _trayIcon.Notify(
            "Kohana sigue activo",
            "Ábrelo con Alt + A o desde el icono de la bandeja.",
            TrayNotificationKind.Information,
            _preferences.ShowWindowsNotifications,
            playSound: false);
    }

    private void RequestExit()
    {
        if (_isClosed)
        {
            return;
        }

        _allowExit = true;
        System.Windows.Application.Current.Shutdown();
    }

    private static string FormatPercentage(double? value)
    {
        return value.HasValue ? $"{value.Value:0}%" : "—";
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

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
