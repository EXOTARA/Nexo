using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Nexo.App.Ambient;
using Nexo.App.Automation;
using Nexo.App.DailyFlow;
using Nexo.App.Optimization;
using Nexo.App.WindowsIntegration;
using Nexo.App.Views;
using Nexo.Core.Ai;
using Nexo.Core.Ambient;
using Nexo.Core.Assistant;
using Nexo.Core.Audit;
using Nexo.Core.AdaptiveEngine;
using Nexo.Core.Automation;
using Nexo.Core.Audio;
using Nexo.Core.Commands;
using Nexo.Core.Commands.CommandCenter;
using Nexo.Core.ComputerUse;
using Nexo.Core.Diagnostics;
using Nexo.Core.Flow;
using Nexo.Core.Focus;
using Nexo.Core.Hardware;
using Nexo.Core.Memory;
using Nexo.Core.Metrics;
using Nexo.Core.Optimization;
using Nexo.Core.Permissions;
using Nexo.Core.Productization;
using Nexo.Core.Resources;
using Nexo.Core.Settings;
using Nexo.Core.Shell;
using Nexo.Core.Skills;
using Nexo.Core.Tasks;
using Nexo.Core.Voice;
using Nexo.Core.Vision;
using Nexo.Core.Workspace;
using Nexo.Windows.Ai;
using Nexo.Windows.Ambient;
using Nexo.Windows.Audit;
using Nexo.Windows.Diagnostics;
using Nexo.Windows.ComputerUse;
using Nexo.Windows.Automation;
using Nexo.Windows.Assistant;
using Nexo.Windows.Audio;
using Nexo.Windows.Flow;
using Nexo.Windows.Focus;
using Nexo.Windows.Memory;
using Nexo.Windows.Metrics;
using Nexo.Windows.Optimization;
using Nexo.Windows.Productization;
using Nexo.Windows.Resources;
using Nexo.Windows.Settings;
using Nexo.Windows.Skills;
using Nexo.Windows.Tasks;
using Nexo.Windows.Voice;
using Nexo.Windows.Vision;
using Nexo.Windows.WindowsIntegration;
using Nexo.Windows.Workspace;
using NexoFocusManager = Nexo.Core.Focus.FocusManager;

namespace Nexo.App;

public partial class MainWindow : Window
{
    private const int ShellHotkeyId = 0x4E58;
    private const int PeekHotkeyId = 0x4E59;
    private const int CommandPaletteHotkeyId = 0x4E5A;
    private const int LookHotkeyId = 0x4E5B;

    /// <summary>Diseño D6.3 (Fase 3 — Kohana Flow) — atajo global de dictado.</summary>
    private const int FlowHotkeyId = 0x4E5C;

    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint VirtualKeyA = 0x41;
    private const uint VirtualKeySpace = 0x20;
    private const uint VirtualKeyD = 0x44;
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
    private readonly IScreenCaptureService _screenCaptureService;
    private readonly VoiceCoordinator _voiceCoordinator;
    private readonly IHardwareCapabilityService _hardwareCapabilityService;
    private readonly IAdaptiveEngineRegistry _adaptiveEngineRegistry;
    private OllamaRuntimeSnapshot? _latestOllamaRuntimeSnapshot;
    private readonly SemaphoreSlim _aiGate = new(1, 1);

    // Serializa las DECISIONES del Resource Governor (pausar/reanudar wake word según el
    // modo de recursos y la bandera _resourceGovernorWakeWordPaused), no el acceso físico
    // a Vosk: las operaciones reales del motor pasan después por el ámbito de wake word del
    // coordinador (vía PauseWakeWordAsync/ApplyWakeWordPreferenceAsync). No es un candado
    // del subsistema de voz —esos viven en VoiceCoordinator—; por eso vive en MainWindow.
    private readonly SemaphoreSlim _resourceGovernorDecisionGate = new(1, 1);
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
    private readonly JsonAmbientRequestHistoryStore _ambientRequestStore = new();
    private readonly AmbientRequestManager _ambientRequestManager;
    private readonly WindowsAmbientContextProvider _ambientContextProvider = new();

    /// <summary>
    /// Diseño D4 (corrección post smoke test manual) — fuente propia y en tiempo real de "última
    /// ventana ajena en primer plano" para el Context Snapshot ambiental. Deliberadamente
    /// independiente de <see cref="_lastExternalWindowHandle"/> (mecanismo existente de Vision/
    /// Peek, que solo se actualiza en puntos concretos como <see cref="RememberForegroundWindow"/>
    /// y no ante un Alt+Tab normal) para no alterar ese comportamiento ya probado.
    /// </summary>
    private readonly ForegroundWindowTracker _ambientForegroundTracker = new();

    /// <summary>Diseño D5 (Fase 2 — Kohana Lens) — OCR y lectura de UI Automation, ambos sin estado.</summary>
    private readonly WindowsOcrService _lensOcrService = new();
    private readonly WindowsUiAutomationReader _lensUiAutomationReader = new();
    private readonly LensHighlightOverlay _lensHighlightOverlay = new();

    /// <summary>Diseño D8 (Fase 4) — optimización adaptativa del equipo.</summary>
    private readonly WindowsOptimizationApplier _optimizationApplier = new();
    private readonly JsonOptimizationSnapshotStore _optimizationSnapshotStore = new();
    /// <summary>
    /// Diseño D13 — el Audit Log único: qué hizo Kohana, cuándo, con qué permiso y cómo deshacerlo.
    /// Lo comparten todas las capacidades; un registro por capacidad obligaría a la persona a saber
    /// de antemano en cuál mirar.
    /// </summary>
    private readonly JsonKohanaAuditLog _auditLog = new();

    /// <summary>
    /// Diseño D11 (Fase 4) — orquesta aplicar, verificar, deshacer y registrar. Se crea en el
    /// constructor porque necesita las preferencias ya cargadas para el objetivo "consumo de
    /// Kohana".
    /// </summary>
    private readonly OptimizationCoordinator _optimizationCoordinator;

    /// <summary>Diseño D9 (Fase 6) — memoria opt-in, cifrada en reposo.</summary>
    private readonly MemoryManager _memoryManager = new(new DpapiMemoryStore());

    /// <summary>
    /// Diseño D10 — recuerdo propuesto que espera un sí. Nunca se guarda solo: mientras vive aquí
    /// no está en la memoria.
    /// </summary>
    private MemoryCandidate? _pendingMemoryCandidate;

    /// <summary>Diseño D12 (Fase 5) — lectura de solo-lectura del proyecto autorizado.</summary>
    private readonly FileSystemWorkspaceReader _workspaceReader = new();

    /// <summary>
    /// Diseño D12 — contexto del proyecto para la SIGUIENTE consulta, y solo para ésa. No se manda
    /// en todas: el proyecto solo sale del equipo cuando la persona pidió algo sobre el proyecto.
    /// </summary>
    private string? _pendingWorkspaceContext;

    /// <summary>Diseño D13 — la paleta no acepta argumentos: la consulta de búsqueda llega por chat.</summary>
    private bool _awaitingWorkspaceSearchQuery;

    /// <summary>Diseño D17 (Fase 7) — qué métodos puede usar Kohana para actuar en este equipo.</summary>
    private readonly WindowsComputerUseMethodProbe _computerUseProbe = new();

    /// <summary>Diseño D17 — igual que la búsqueda: lo que se quiere hacer llega por chat.</summary>
    private bool _awaitingComputerUseIntent;

    /// <summary>Diseño D18 (Fase 7, nivel 4) — el único camino por el que Kohana actúa en el equipo.</summary>
    private readonly ComputerUseCoordinator _computerUseCoordinator;

    /// <summary>Diseño D20 (Fase 9) — copia verificada de los datos, para poder volver.</summary>
    private readonly FileSystemDataBackupService _backupService = new();

    /// <summary>Diseño D21 (Fase 9) — el mismo diagnóstico que ve la ventana, para poder exportarlo.</summary>
    private readonly NexoDiagnosticService _diagnosticService = new();

    private readonly UpdateSafetyCoordinator _updateSafety;

    /// <summary>Diseño D14 — igual que la búsqueda: la instrucción del cambio llega por chat.</summary>
    private bool _awaitingWorkspaceEditInstruction;

    /// <summary>
    /// Diseño D14 — la SIGUIENTE respuesta de la IA puede traer un cambio propuesto. Solo esa: sin
    /// esta marca, cualquier respuesta que por casualidad contuviera el formato ofrecería escribir
    /// en el proyecto, y nadie lo habría pedido.
    /// </summary>
    private bool _pendingWorkspaceEditRequested;

    /// <summary>
    /// Diseño D14 (Fase 5, nivel 4) — checkpoints y escritura. Existen siempre, pero
    /// <see cref="WorkspaceEditCoordinator"/> se niega a escribir por debajo del nivel 4, así que
    /// tenerlos construidos no concede nada.
    /// </summary>
    private readonly JsonWorkspaceCheckpointStore _workspaceCheckpointStore = new();

    private readonly WorkspaceEditCoordinator _workspaceEditCoordinator;

    /// <summary>Diseño D15 (Fase 8) — packs que dejan configuradas capacidades ya existentes.</summary>
    private readonly SkillPackCoordinator _skillPackCoordinator;

    /// <summary>Diseño D6 (Fase 3 — Kohana Flow) — dictado global.</summary>
    private readonly WindowsFlowTextInserter _flowTextInserter;

    /// <summary>
    /// Handle de la ventana donde se escribirá lo dictado, recordado AL EMPEZAR. Si cambia el foco
    /// antes de terminar, <see cref="WindowsFlowTextInserter"/> se niega a escribir.
    /// </summary>
    private long _flowTargetWindowHandle;

    /// <summary>
    /// Señal que cierra el dictado en curso. El atajo llega como dos pulsaciones separadas, pero el
    /// ámbito de voz debe sostenerse durante toda la sección crítica; por eso una única operación
    /// asíncrona espera aquí en vez de guardar el ámbito en un campo entre dos manejadores.
    /// </summary>
    private TaskCompletionSource<bool>? _flowStopSignal;
    private readonly SakuraPillWindow _sakuraPillWindow = new();
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

    /// <summary>
    /// Diseño D2 — Sakura Command Center. Se crea la primera vez que se abre (Ctrl + K o el botón
    /// del encabezado) para no alargar el arranque con una ventana que puede no usarse.
    /// </summary>
    private CommandCenterWindow? _commandCenterWindow;

    /// <summary>
    /// Diseño D4.4 — visor del historial de solicitudes ambientales. Igual que
    /// <see cref="_commandCenterWindow"/>, perezoso: se crea la primera vez que se pide desde el
    /// Command Center.
    /// </summary>
    private AmbientHistoryWindow? _ambientHistoryWindow;

    /// <summary>
    /// Diseño D3 — ver <see cref="DailyFlowEventHub"/>. Reenvía los eventos de dominio que ya
    /// existían (TasksChanged/FocusChanged/rutina ejecutada) a un solo punto de extensión, sin
    /// reemplazar los manejadores existentes.
    /// </summary>
    private readonly DailyFlowEventHub _dailyFlowHub = new();

    /// <summary>
    /// Diseño D3.1 — conecta el mini temporizador global del encabezado con FocusManager y la
    /// navegación, sin acumular esa lógica en MainWindow. Se construye en el constructor porque
    /// el mini temporizador es parte fija del shell (a diferencia del Command Center, que se crea
    /// de forma perezosa).
    /// </summary>
    private readonly FocusContinuityCoordinator _focusContinuity;

    /// <summary>
    /// Diseño D4 — conecta <see cref="AmbientRequestManager"/> con el Sakura Pill Host
    /// (<see cref="_sakuraPillWindow"/>), igual que <see cref="_focusContinuity"/> conecta
    /// <c>FocusManager</c> con el mini temporizador.
    /// </summary>
    private readonly SakuraPillCoordinator _ambientCoordinator;
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
    private bool _exitRequested;
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
        IScreenCaptureService? screenCaptureService = null,
        VoiceCoordinator? voiceCoordinator = null,
        IHardwareCapabilityService? hardwareCapabilityService = null,
        IAdaptiveEngineRegistry? adaptiveEngineRegistry = null)
    {
        InitializeComponent();
        _commandPaletteWindow = new CommandPaletteWindow();

        // Todos los servicios son dependencias obligatorias provistas por la raíz de
        // composición (App.OnStartup): MainWindow nunca construye un motor. Se asignan aquí,
        // antes de cualquier campo dependiente y antes de cablear eventos. El coordinador de
        // voz es el único punto de acceso al subsistema de voz; MainWindow no recibe los tres
        // motores (Whisper, TTS, Vosk), que posee y libera KohanaCompositionRoot.
        _aiChatService = aiChatService ?? throw new ArgumentNullException(nameof(aiChatService));
        _audioMixerService = audioMixerService ?? throw new ArgumentNullException(nameof(audioMixerService));
        _screenCaptureService = screenCaptureService ?? throw new ArgumentNullException(nameof(screenCaptureService));
        _voiceCoordinator = voiceCoordinator ?? throw new ArgumentNullException(nameof(voiceCoordinator));
        _hardwareCapabilityService = hardwareCapabilityService ?? throw new ArgumentNullException(nameof(hardwareCapabilityService));
        _adaptiveEngineRegistry = adaptiveEngineRegistry ?? throw new ArgumentNullException(nameof(adaptiveEngineRegistry));

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
        _ambientRequestManager = new AmbientRequestManager(_ambientRequestStore);
        _ambientRequestManager.Load();
        _flowTextInserter = new WindowsFlowTextInserter(_ambientContextProvider);
        _memoryManager.Load();

        // Diseño D11 — el coordinador necesita las preferencias ya cargadas: el segundo objetivo
        // que aplica de verdad es el modo de rendimiento de la propia Kohana.
        _optimizationCoordinator = new OptimizationCoordinator(
            _optimizationApplier,
            new PreferencesKohanaFootprintApplier(
                _preferences,
                () =>
                {
                    SavePreferences();
                    RefreshAdaptiveEnginePlan();
                }),
            _optimizationSnapshotStore,
            _auditLog);

        _workspaceEditCoordinator = new WorkspaceEditCoordinator(
            new FileSystemWorkspaceWriter(),
            _workspaceCheckpointStore,
            _auditLog);

        _skillPackCoordinator = new SkillPackCoordinator(
            new JsonSkillPackSnapshotStore(),
            _auditLog);

        _computerUseCoordinator = new ComputerUseCoordinator(
            new WindowsComputerUseExecutor(),
            _computerUseProbe,
            new JsonComputerUseSnapshotStore(),
            _auditLog,

            // Diseño D19 — el invocador comparte el lector de Lens, pero no la capacidad: leer un
            // control e invocarlo pasan por interfaces y permisos distintos.
            new WindowsUiAutomationInvoker(_lensUiAutomationReader));

        _updateSafety = new UpdateSafetyCoordinator(_backupService, _auditLog);

        _tasksView = new TasksView(_taskManager);
        _focusView = new FocusView(
            _focusManager,
            taskId => _taskManager.GetAll().FirstOrDefault(task => task.Id == taskId)?.Title);
        _routinesView = new RoutinesView(_routineManager);
        _audioView = new AudioView(_audioMixerService);

        _focusContinuity = new FocusContinuityCoordinator(
            _focusManager,
            FocusMiniTimerControl,
            taskId => _taskManager.GetAll().FirstOrDefault(task => task.Id == taskId)?.Title,
            () => NavigateTo(ShellNavigationPolicy.Focus, animate: _preferences.AnimationsEnabled));
        _focusContinuity.FinishRequested += (_, _) => FinishActiveFocusSession();

        _ambientCoordinator = new SakuraPillCoordinator(_ambientRequestManager, _sakuraPillWindow);
        _ambientCoordinator.QuickActionInvoked += AmbientCoordinator_QuickActionInvoked;

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
        _tasksView.FocusRequested += TasksView_FocusRequested;
        _focusView.FocusChanged += FocusView_FocusChanged;
        _focusView.CompleteAssociatedTaskRequested += FocusView_CompleteAssociatedTaskRequested;
        _routinesView.ExecuteRequested += RoutinesView_ExecuteRequested;
        // Los eventos de wake word se suscriben a través del coordinador (paso directo al
        // servicio subyacente): MainWindow ya no necesita una referencia al servicio.
        _voiceCoordinator.WakeWordDetected += WakeWordService_WakeWordDetected;
        _voiceCoordinator.RecognitionObserved += WakeWordService_RecognitionObserved;
        _voiceCoordinator.WakeWordCustomAliases = _preferences.WakeWordAliases;
        _audioView.ActionCompleted += AudioView_ActionCompleted;
        _captureView.CaptureRequested += CaptureView_CaptureRequested;
        _commandPaletteWindow.PromptSubmitted += CommandPaletteWindow_PromptSubmitted;
        _commandPaletteWindow.WorkspaceRequested += CommandPaletteWindow_WorkspaceRequested;
        _homeView.CommandRequested += HomeView_CommandRequested;
        _homeView.TasksRequested += HomeView_TasksRequested;
        _homeView.FocusRequested += HomeView_FocusRequested;
        _homeView.RoutinesRequested += HomeView_RoutinesRequested;
        _homeView.ContextRequested += HomeView_ContextRequested;
        _homeView.NewTaskRequested += HomeView_NewTaskRequested;
        _homeView.StartFocusRequested += HomeView_StartFocusRequested;
        _homeView.PauseFocusRequested += (_, _) => { _focusManager.Pause(DateTimeOffset.Now); CheckFocusTimer(); };
        _homeView.ResumeFocusRequested += (_, _) => { _focusManager.Resume(DateTimeOffset.Now); CheckFocusTimer(); };
        _homeView.CommandCenterRequested += (_, _) => ShowCommandCenter();
        _systemView.RestartVoiceRequested += async (_, _) => await RestartWakeWordAsync();
        _systemView.DiagnosticsRequested += (_, _) => ShowDiagnostics();
        _systemView.HardwareCapabilityRefreshRequested += async (_, _) => await RefreshHardwareCapabilityAsync();

        // Diseño D11 (Fase 4) — los mismos siete escenarios que ya existían como comandos, ahora
        // también en Sistema. Comparten camino: la interfaz no aplica nada por su cuenta.
        _systemView.OptimizationScenarioRequested += async scenario =>
            await ProposeOptimizationAsync(scenario);
        _systemView.OptimizationUndoRequested += (_, _) => RestoreOptimization();
        _systemView.OptimizationAuditRequested += (_, _) => ShowOptimizationAudit();
        _systemView.AuditRefreshRequested += (_, _) => RefreshAuditPanel();
        _systemView.AuditRevertRequested += RevertAuditEntry;
        _assistantView.ConfigureHistory(
            _preferences.SaveConversationHistory,
            _preferences.RecentConversationMessageLimit);

        if (_preferences.SaveConversationHistory)
        {
            _assistantView.LoadConversation(_conversationStore.Load());
        }

        WireSettingsEvents();
        _settingsView.ApplyPreferences(_preferences);

        // Diseño D13 — la auditoría se enseña ya poblada. Un panel vacío al abrir haría pensar que
        // no hay registro, cuando lo que pasa es que nadie lo ha pedido todavía.
        RefreshAuditPanel();
        _systemView.SetOptimizationStatus(detail: null, _optimizationCoordinator.HasSomethingToUndo);
        UpdateAiProviderStatus();
        ApplyPreferences();
        _voiceCoordinator.WakeWordSensitivity = _preferences.WakeWordSensitivity;
        _assistantView.SetVisionAvailability(_preferences.VisionEnabled);
        ConfigureVoiceInputDevices();
        NavigateTo(ShellNavigationPolicy.DefaultDestination, animate: false);
        SetSideRailExpanded(_preferences.SideRailExpanded, animate: false, persist: false);
        UpdateResourceModeIndicator(ResourceGovernorDecision.Normal);
        RefreshRuntimeDashboard();
        _ = RefreshHardwareCapabilityAsync();

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
                _voiceCoordinator.StopSpeaking();
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
            RefreshAdaptiveEnginePlan();
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
            _voiceCoordinator.WakeWordSensitivity = sensitivity;
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

        // Diseño D7 (Fase 3 — Kohana Flow)
        _settingsView.FlowEnabledChanged += enabled =>
        {
            _preferences.FlowEnabled = enabled;
            SavePreferences();
            ApplyFlowHotkeyRegistration();
        };

        _settingsView.FlowModeChanged += mode =>
        {
            _preferences.FlowMode = mode;
            SavePreferences();
        };

        _settingsView.FlowDictionaryChanged += lines =>
        {
            _preferences.FlowDictionary = [.. lines];
            SavePreferences();
        };

        _settingsView.FlowSnippetsChanged += lines =>
        {
            _preferences.FlowSnippets = [.. lines];
            SavePreferences();
        };

        // Diseño D10 (Fase 6 — Context and Memory)
        _settingsView.MemoryEnabledChanged += enabled =>
        {
            _preferences.Memory.Enabled = enabled;

            // Normalize() apaga también las categorías cuando el interruptor general se apaga. Se
            // llama aquí, y no solo al guardar, para que la política vea el estado correcto en la
            // siguiente frase aunque el guardado tarde.
            _preferences.Memory.Normalize();
            SavePreferences();
            _settingsView.ApplyMemorySettings(_preferences.Memory);
            _settingsView.SetMemoryStatus(enabled
                ? "Memoria activada. Elige qué categorías puede recordar."
                : "Memoria desactivada. Lo ya guardado sigue ahí hasta que lo borres.");
        };

        _settingsView.MemoryCategoryChanged += (category, enabled) =>
        {
            switch (category)
            {
                case MemoryCategory.Preferencias:
                    _preferences.Memory.RememberPreferences = enabled;
                    break;
                case MemoryCategory.Conversacion:
                    _preferences.Memory.RememberConversation = enabled;
                    break;
                case MemoryCategory.Habitos:
                    _preferences.Memory.RememberHabits = enabled;
                    break;
            }

            SavePreferences();
        };

        _settingsView.MemoryRetentionChanged += days =>
        {
            _preferences.Memory.RetentionDays = days;
            SavePreferences();

            // La retención se aplica al leer, así que basta con pedir la lista para que lo que ya
            // caducó desaparezca ahora mismo y no en la próxima escritura.
            var remaining = _memoryManager.GetAll(_preferences.Memory, DateTimeOffset.Now).Count;
            _settingsView.SetMemoryStatus(
                $"Retención de {days} días. Quedan {remaining} recuerdos guardados.");
        };

        _settingsView.MemoryExclusionsChanged += lines =>
        {
            _preferences.Memory.Exclusions = [.. lines];
            _preferences.Memory.Normalize();
            SavePreferences();
            _settingsView.SetMemoryStatus(
                _preferences.Memory.Exclusions.Count == 0
                    ? "Sin exclusiones."
                    : $"{_preferences.Memory.Exclusions.Count} exclusiones activas. " +
                      "No afectan a lo ya guardado: para eso, usa «olvidar todo».");
        };

        _settingsView.MemoryShowRequested += (_, _) =>
        {
            ShowMemoryContents();
            NavigateTo("Assistant", animate: true);
        };

        _settingsView.MemoryForgetAllRequested += (_, _) =>
        {
            var result = ForgetAllMemory();
            _settingsView.SetMemoryStatus(result.Message);
        };

        // Diseño D16 — cambiar un permiso es la decisión de la que cuelgan las demás, así que se
        // confirma al ampliar y se registra siempre.
        _settingsView.CapabilityPermissionChanged += (capability, level) =>
        {
            var permission = _preferences.Permissions.For(capability);
            var previous = permission.Level;

            if (previous == level)
            {
                return;
            }

            // Política de mínimo privilegio: ampliar exige una confirmación nueva; restringir, no.
            if (PermissionBroker.RequiresNewConfirmation(previous, level))
            {
                var confirmation = MessageBox.Show(
                    this,
                    $"Vas a ampliar el permiso de «{CapabilityText.Describe(capability)}» " +
                        $"de {previous} a {level}." + Environment.NewLine + Environment.NewLine +
                        "Aunque lo permitas, seguiré preguntándote antes de borrar algo sin " +
                        "recuperación, tocar credenciales, enviar algo fuera de tu equipo o pedir " +
                        "permisos de administrador." + Environment.NewLine + Environment.NewLine +
                        "¿Lo amplío?",
                    "Ampliar un permiso",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirmation != MessageBoxResult.Yes)
                {
                    _settingsView.ApplyPermissionSettings(_preferences.Permissions);
                    _settingsView.SetPermissionsStatus("No cambié ese permiso.");
                    return;
                }
            }

            permission.Level = level;
            SavePreferences();

            RecordAudit(
                AuditCapability.Permisos,
                $"Permiso cambiado ({capability})",
                $"{previous} → {level}",
                "Cambio explícito del usuario");

            _settingsView.SetPermissionsStatus(
                $"«{CapabilityTitleFor(capability)}» quedó en {level}.");
        };

        // Diseño D19 — las exclusiones por aplicación, ya editables sin abrir settings.json.
        _settingsView.PermissionExclusionsChanged += lines =>
        {
            PermissionExclusionParser.Apply(_preferences.Permissions, lines);
            _preferences.Permissions.Normalize();
            SavePreferences();

            var total = _preferences.Permissions.Capabilities.Sum(entry => entry.ExcludedApps.Count);
            RecordAudit(
                AuditCapability.Permisos,
                "Exclusiones por aplicación actualizadas",
                total == 0 ? "Sin exclusiones." : $"{total} exclusiones activas.",
                "Cambio explícito del usuario");

            _settingsView.ApplyPermissionSettings(_preferences.Permissions);
        };

        // Diseño D18 — subir hasta "ejecutar un paso" en el equipo se confirma aparte del permiso:
        // son dos decisiones distintas y la más arriesgada del roadmap merece las dos.
        _settingsView.ComputerUseAutonomyLevelChanged += level =>
        {
            var previous = _preferences.ComputerUseAutonomyLevel;
            if (previous == level)
            {
                return;
            }

            if (level > previous)
            {
                var confirmation = MessageBox.Show(
                    this,
                    $"Vas a subir lo que Kohana puede hacer en tu equipo de {previous} a {level}." +
                        Environment.NewLine + Environment.NewLine +
                        (level == AutonomyLevel.EjecutarUnPaso
                            ? "Podrá ejecutar UNA acción cada vez, y te la confirmaré antes. Sigo " +
                              "eligiendo siempre la forma más segura disponible, y sigo sin usar " +
                              "ratón y teclado simulados."
                            : "Seguirá sin ejecutar nada.") +
                        Environment.NewLine + Environment.NewLine + "¿Lo subo?",
                    "Subir el nivel en el equipo",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirmation != MessageBoxResult.Yes)
                {
                    _settingsView.ApplyComputerUseAutonomyLevel(previous);
                    return;
                }
            }

            _preferences.ComputerUseAutonomyLevel = level;
            SavePreferences();

            RecordAudit(
                AuditCapability.Permisos,
                "Nivel en el equipo cambiado",
                $"{previous} → {level}",
                "Elección explícita del usuario",
                (int)level);
        };

        // Diseño D13 (Fase 5 — Project Companion)
        _settingsView.WorkspaceAuthorizeRequested += (_, _) =>
        {
            AuthorizeWorkspaceFolder();
            _settingsView.ApplyWorkspaceSettings(_preferences.Workspace);
        };

        _settingsView.WorkspaceRevokeRequested += (_, _) =>
        {
            RevokeWorkspace();
            _settingsView.ApplyWorkspaceSettings(_preferences.Workspace);
        };

        _settingsView.WorkspaceAutonomyLevelChanged += level =>
        {
            _preferences.Workspace.AutonomyLevel = level;
            _preferences.Workspace.Normalize();
            SavePreferences();

            // Cambiar hasta dónde puede llegar Kohana es una decisión de permisos, así que se
            // registra igual que autorizar la carpeta.
            RecordAudit(
                AuditCapability.Permisos,
                "Nivel de autonomía del proyecto cambiado",
                WorkspaceAutonomyPolicy.Describe(_preferences.Workspace.AutonomyLevel),
                "Elección explícita del usuario",
                (int)_preferences.Workspace.AutonomyLevel);
        };

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
            RefreshAdaptiveEnginePlan();
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

        _settingsView.HardwarePerformanceModeChanged += mode =>
        {
            _preferences.HardwarePerformanceMode = mode;
            SavePreferences();
            RefreshAdaptiveEnginePlan();
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

        // Diseño D2 — restaurar apariencia. Solo se reaplican las preferencias visuales; las
        // funcionales (tareas, rutinas, voz, IA, motores, integración con Windows) ni se leen ni
        // se escriben aquí, que es justo lo que hace segura esta acción.
        _settingsView.ResetAppearanceRequested += (_, _) =>
        {
            _preferences.ResetVisualPreferences();

            Width = _preferences.Width;
            PositionWindow();
            _peekWindow.HideImmediately();
            ApplyShellOpacity();
            ApplyAccent(_preferences.AccentColor);
            SetSideRailExpanded(_preferences.SideRailExpanded, animate: false, persist: false);
            UpdateNavigationState(_currentDestination);

            // Refresca los controles de Personalizar para que reflejen los valores restaurados.
            _settingsView.ApplyPreferences(_preferences);

            SavePreferences();
            _assistantView.AddKohanaMessage(
                "Apariencia restaurada. Tus tareas, rutinas y la configuración de voz, IA y motores no se tocaron.");
        };
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
            _voiceCoordinator.GetInputDevices(),
            _voiceCoordinator.IsVoiceInputReady,
            _voiceCoordinator.IsWakeWordReady,
            _voiceCoordinator.IsWakeWordListening,
            trayActive: true,
            _startupService.IsEnabled(),
            _hardwareCapabilityService,
            _adaptiveEngineRegistry)
        {
            Owner = this
        };
        window.ShowDialog();
    }

    private async Task ShowOnboardingAsync()
    {
        await PauseWakeWordAsync();
        _voiceCoordinator.StopSpeaking();

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

    /// <summary>
    /// Diseño D1 (Sakura Shell): las animaciones del shell respetan tanto la preferencia propia
    /// de Kohana como la preferencia de Windows (Configuración de accesibilidad → Efectos
    /// visuales). Si cualquiera de las dos está desactivada, los cambios de estado se aplican
    /// de inmediato, sin transición.
    /// </summary>
    private bool ShellAnimationsAllowed => _preferences.AnimationsEnabled && SystemParameters.ClientAreaAnimation;

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

        var targetWidth = expanded
            ? (double)FindResource("SidebarWidthExpanded")
            : (double)FindResource("SidebarWidthCollapsed");

        if (!animate || !ShellAnimationsAllowed)
        {
            SideRailBorder.BeginAnimation(FrameworkElement.WidthProperty, null);
            SideRailBorder.Width = targetWidth;
            return;
        }

        var currentWidth = SideRailBorder.ActualWidth > 0
            ? SideRailBorder.ActualWidth
            : SideRailBorder.Width;
        var easing = (CubicEase)FindResource("MotionEaseOut");
        var animation = new DoubleAnimation(
            currentWidth,
            targetWidth,
            (Duration)FindResource("MotionBase"))
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
        var buttonWidth = expanded
            ? (double)FindResource("SidebarButtonWidthExpanded")
            : (double)FindResource("SidebarButtonWidthCollapsed");
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

    private void CommandCenterButton_Click(object sender, RoutedEventArgs e)
    {
        ShowCommandCenter();
    }

    /// <summary>
    /// Diseño D2 — abre el Sakura Command Center (Ctrl + K). Se crea de forma perezosa la primera
    /// vez: construir la ventana y el registro en el arranque solo retrasaría el inicio de Kohana
    /// para una función que puede no usarse en toda la sesión.
    /// </summary>
    private void ShowCommandCenter()
    {
        if (_isClosed)
        {
            return;
        }

        if (_commandCenterWindow is null)
        {
            _commandCenterWindow = new CommandCenterWindow(BuildCommandRegistry());
            _commandCenterWindow.CommandFailed += CommandCenterWindow_CommandFailed;
        }
        else
        {
            // Diseño D3: algunos comandos son dinámicos (uno por rutina habilitada) — se
            // reconstruye el registro en cada apertura para que nunca muestre una rutina ya
            // renombrada, deshabilitada o eliminada desde la última vez.
            _commandCenterWindow.UpdateCommands(BuildCommandRegistry());
        }

        _commandCenterWindow.ShowFor(this, Keyboard.FocusedElement);
    }

    private void CommandCenterWindow_CommandFailed(object? sender, CommandCenterFailureEventArgs e)
    {
        // Un comando fallido no cierra Kohana ni se informa como éxito: se avisa en la
        // conversación (no modal) y el detalle técnico va al diagnóstico.
        _assistantView.AddKohanaMessage(e.Result.Message ?? "No se pudo completar la acción.");

        if (e.Result.Error is { } error)
        {
            WriteCommandCenterLog(e.Command.Id, error);
        }
    }

    /// <summary>
    /// Registra el fallo de un comando conservando tipo, mensaje, excepción interna y stack trace,
    /// como exige el encargo. Sigue el mismo patrón que el resto de registros de Kohana: escribir
    /// un log nunca puede afectar al funcionamiento de la aplicación.
    /// </summary>
    private static void WriteCommandCenterLog(string commandId, Exception error)
    {
        try
        {
            Directory.CreateDirectory(Nexo.Core.Diagnostics.NexoDataPaths.LogsDirectory);
            File.AppendAllText(
                Nexo.Core.Diagnostics.NexoDataPaths.CommandCenterLog,
                $"{DateTimeOffset.Now:O} | comando '{commandId}' | " +
                $"{error.GetType().FullName}: {error.Message}{Environment.NewLine}" +
                $"{error}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // El registro no debe afectar el funcionamiento de Kohana.
        }
        catch (UnauthorizedAccessException)
        {
            // El registro no debe afectar el funcionamiento de Kohana.
        }
    }

    /// <summary>
    /// Construye el registro de comandos enlazando cada uno a un servicio real ya existente.
    /// No se inventa ninguna acción: solo se exponen cosas que el shell ya sabe hacer.
    /// </summary>
    private KohanaCommandRegistry BuildCommandRegistry()
    {
        var registry = new KohanaCommandRegistry();

        registry.RegisterRange(BuildNavigationCommands());

        registry.Register(new KohanaCommandDescriptor(
            "shell.sidebar.toggle",
            "Alternar barra lateral",
            "Expande o contrae la navegación lateral.",
            KohanaCommandCategory.Shell,
            _ =>
            {
                SetSideRailExpanded(!_sideRailExpanded, animate: _preferences.AnimationsEnabled);
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["barra", "lateral", "sidebar", "contraer", "expandir", "navegación"]));

        registry.Register(new KohanaCommandDescriptor(
            "focus.open",
            "Abrir Enfoque",
            "Va a la sección de Enfoque.",
            KohanaCommandCategory.Focus,
            _ =>
            {
                NavigateTo(ShellNavigationPolicy.Focus, animate: _preferences.AnimationsEnabled);
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["enfoque", "concentración", "pomodoro", "sesión", "abrir"]));

        registry.RegisterRange(BuildFocusStartCommands());

        registry.Register(new KohanaCommandDescriptor(
            "focus.cancel",
            "Cancelar sesión de enfoque",
            "Descarta la sesión en curso sin registrarla en el historial.",
            KohanaCommandCategory.Focus,
            _ =>
            {
                var result = _focusManager.Cancel();
                CheckFocusTimer();
                return Task.FromResult(result.Success
                    ? CommandExecutionResult.Success(result.Message)
                    : CommandExecutionResult.Failure(result.Message));
            },
            keywords: ["enfoque", "cancelar", "descartar", "detener"],
            availability: () => _focusManager.GetSnapshot(DateTimeOffset.Now).ActiveTimer is not null
                ? KohanaCommandAvailability.Available
                : KohanaCommandAvailability.Unavailable("No hay ninguna sesión de enfoque activa.")));

        registry.Register(new KohanaCommandDescriptor(
            "focus.pause",
            "Pausar sesión de enfoque",
            "Pausa la sesión en curso, conservando el tiempo restante.",
            KohanaCommandCategory.Focus,
            _ =>
            {
                var result = _focusManager.Pause(DateTimeOffset.Now);
                CheckFocusTimer();
                return Task.FromResult(result.Success
                    ? CommandExecutionResult.Success(result.Message)
                    : CommandExecutionResult.Failure(result.Message));
            },
            keywords: ["enfoque", "pausar", "detener"],
            availability: () => _focusManager.GetSnapshot(DateTimeOffset.Now).ActiveTimer is { Status: FocusTimerStatus.Running }
                ? KohanaCommandAvailability.Available
                : KohanaCommandAvailability.Unavailable("No hay ninguna sesión de enfoque en curso para pausar.")));

        registry.Register(new KohanaCommandDescriptor(
            "focus.resume",
            "Continuar sesión de enfoque",
            "Reanuda la sesión pausada.",
            KohanaCommandCategory.Focus,
            _ =>
            {
                var result = _focusManager.Resume(DateTimeOffset.Now);
                CheckFocusTimer();
                return Task.FromResult(result.Success
                    ? CommandExecutionResult.Success(result.Message)
                    : CommandExecutionResult.Failure(result.Message));
            },
            keywords: ["enfoque", "continuar", "reanudar", "resumir"],
            availability: () => _focusManager.GetSnapshot(DateTimeOffset.Now).ActiveTimer is { Status: FocusTimerStatus.Paused }
                ? KohanaCommandAvailability.Available
                : KohanaCommandAvailability.Unavailable("No hay ninguna sesión de enfoque en pausa.")));

        registry.Register(new KohanaCommandDescriptor(
            "focus.finish",
            "Finalizar sesión de enfoque",
            "Termina la sesión ahora y cuenta el tiempo ya transcurrido, a diferencia de Cancelar.",
            KohanaCommandCategory.Focus,
            _ => Task.FromResult(FinishActiveFocusSession()),
            keywords: ["enfoque", "finalizar", "terminar", "completar"],
            availability: () => _focusManager.GetSnapshot(DateTimeOffset.Now).ActiveTimer is not null
                ? KohanaCommandAvailability.Available
                : KohanaCommandAvailability.Unavailable("No hay ninguna sesión de enfoque activa.")));

        registry.Register(new KohanaCommandDescriptor(
            "focus.history",
            "Mostrar historial de enfoque",
            "Abre Enfoque, donde están las últimas sesiones y el resumen del día.",
            KohanaCommandCategory.Focus,
            _ =>
            {
                NavigateTo(ShellNavigationPolicy.Focus, animate: _preferences.AnimationsEnabled);
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["enfoque", "historial", "actividad", "resumen", "sesiones"]));

        registry.Register(new KohanaCommandDescriptor(
            "ambient.contextPeek",
            "¿Qué ventana tengo activa?",
            "Muestra en el Sakura Pill Host el título y el proceso de la última ventana externa " +
            "que tuviste activa, sin robarle el foco.",
            KohanaCommandCategory.Ambient,
            _ => ExecuteAmbientContextPeekAsync(),
            keywords: ["ventana", "activa", "contexto", "ambiental", "pill", "sakura"],
            availability: () => _ambientRequestManager.GetSnapshot(recentCount: 0).ActiveRequest is null
                ? KohanaCommandAvailability.Available
                : KohanaCommandAvailability.Unavailable("Ya hay una solicitud ambiental en curso.")));

        registry.Register(new KohanaCommandDescriptor(
            "ambient.history",
            "Ver historial de solicitudes ambientales",
            "Abre el historial de solicitudes del Sakura Pill Host, con la opción de deshacer las que aplique.",
            KohanaCommandCategory.Ambient,
            _ =>
            {
                ShowAmbientHistory();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["ambiental", "historial", "solicitudes", "pill", "sakura"]));

        registry.RegisterRange(BuildLensCommands());
        registry.RegisterRange(BuildOptimizationCommands());
        registry.RegisterRange(BuildMemoryCommands());
        registry.RegisterRange(BuildWorkspaceCommands());
        registry.RegisterRange(BuildSkillPackCommands());
        registry.RegisterRange(BuildComputerUseCommands());
        registry.RegisterRange(BuildProductizationCommands());

        registry.Register(new KohanaCommandDescriptor(
            "tasks.create",
            "Crear una tarea",
            "Abre Hoy para añadir una tarea nueva.",
            KohanaCommandCategory.Tasks,
            _ =>
            {
                NavigateTo(ShellNavigationPolicy.Tasks, animate: _preferences.AnimationsEnabled);
                _tasksView.OpenNewEditor();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["tarea", "pendiente", "nueva", "añadir", "hoy"]));

        registry.Register(new KohanaCommandDescriptor(
            "audio.mute",
            "Silenciar audio",
            "Silencia el volumen maestro del equipo.",
            KohanaCommandCategory.Audio,
            _ => Task.FromResult(ApplyMasterMute(muted: true)),
            keywords: ["audio", "silenciar", "mute", "volumen"]));

        registry.Register(new KohanaCommandDescriptor(
            "audio.unmute",
            "Restaurar audio",
            "Quita el silencio del volumen maestro.",
            KohanaCommandCategory.Audio,
            _ => Task.FromResult(ApplyMasterMute(muted: false)),
            keywords: ["audio", "restaurar", "activar", "volumen", "sonido"]));

        registry.Register(new KohanaCommandDescriptor(
            "voice.settings",
            "Abrir configuración de voz",
            "Va a Personalizar, donde vive la configuración de voz.",
            KohanaCommandCategory.System,
            _ =>
            {
                NavigateTo(ShellNavigationPolicy.Settings, animate: _preferences.AnimationsEnabled);
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["voz", "micrófono", "whisper", "wake word", "dictado"]));

        registry.Register(new KohanaCommandDescriptor(
            "engine.settings",
            "Ir a configuración del motor",
            "Muestra en Sistema los motores registrados, el recomendado y el configurado.",
            KohanaCommandCategory.System,
            _ =>
            {
                NavigateTo(ShellNavigationPolicy.System, animate: _preferences.AnimationsEnabled);
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["motor", "engine", "registry", "adaptativo", "recomendado", "rendimiento"]));

        registry.RegisterRange(BuildRoutineExecutionCommands());

        return registry;
    }

    /// <summary>
    /// Diseño D3.1 — un comando por cada preset de duración, solo disponible cuando no hay ya una
    /// sesión de enfoque en curso: nunca deben aparecer dos comandos de "iniciar" e "finalizar/
    /// pausar" incompatibles a la vez como disponibles. Cada uno reusa
    /// <see cref="FocusView.StartPreset"/> (no llama a FocusManager.Start directamente) para no
    /// perder una tarea pendiente de asociar si el usuario ya venía de "Enfocarme" en una tarea.
    /// </summary>
    /// <summary>
    /// Diseño D5.6 (Fase 2 — Kohana Lens) — un comando por modo, no un único comando genérico con
    /// un selector: así cada modo aparece por su propio nombre en la búsqueda, igual que los
    /// presets de Enfoque (<see cref="BuildFocusStartCommands"/>).
    /// </summary>
    private IEnumerable<KohanaCommandDescriptor> BuildLensCommands()
    {
        (LensMode Mode, string Id, string Title, string Description, string[] Keywords)[] presets =
        [
            (LensMode.Soporte, "lens.soporte", "Kohana Lens · Soporte",
                "Observa la ventana activa y explica qué problema hay y cómo resolverlo.",
                ["lens", "soporte", "ayuda", "problema", "observar", "mirando"]),
            (LensMode.Estudio, "lens.estudio", "Kohana Lens · Estudio",
                "Observa la ventana activa y explica qué es y cómo funciona, paso a paso.",
                ["lens", "estudio", "aprender", "explicar", "observar", "mirando"]),
            (LensMode.Desarrollo, "lens.desarrollo", "Kohana Lens · Desarrollo",
                "Observa la ventana activa y analiza el código o error visible.",
                ["lens", "desarrollo", "codigo", "error", "diagnostico", "observar", "mirando"])
        ];

        foreach (var (mode, id, title, description, keywords) in presets)
        {
            yield return new KohanaCommandDescriptor(
                id,
                title,
                description,
                KohanaCommandCategory.Capture,
                _ => ExecuteLensAsync(mode),
                keywords: keywords,
                availability: () => _ambientRequestManager.GetSnapshot(recentCount: 0).ActiveRequest is null
                    ? KohanaCommandAvailability.Available
                    : KohanaCommandAvailability.Unavailable("Ya hay una solicitud ambiental en curso."));
        }
    }

    /// <summary>
    /// Diseño D8 (Fase 4) — un comando por escenario. Todos PROPONEN: muestran el plan y lo que
    /// cambiaría, sin tocar nada. Aplicar es un segundo paso explícito, porque cambiar la
    /// configuración del sistema es, en el modelo de confianza, riesgo alto con snapshot previo
    /// obligatorio.
    /// </summary>
    private IEnumerable<KohanaCommandDescriptor> BuildOptimizationCommands()
    {
        (OptimizationScenario Scenario, string Id, string Title)[] presets =
        [
            (OptimizationScenario.Jugar, "optimize.jugar", "Optimizar para jugar"),
            (OptimizationScenario.Programar, "optimize.programar", "Optimizar para programar"),
            (OptimizationScenario.EdicionVideo, "optimize.video", "Optimizar para editar video"),
            (OptimizationScenario.Videollamada, "optimize.videollamada", "Optimizar para videollamada"),
            (OptimizationScenario.Bateria, "optimize.bateria", "Optimizar para batería"),
            (OptimizationScenario.General, "optimize.general", "Optimizar para uso general")
        ];

        foreach (var (scenario, id, title) in presets)
        {
            yield return new KohanaCommandDescriptor(
                id,
                title,
                "Revisa tu hardware real y propone los cambios que tengan sentido. No aplica nada sin que lo confirmes.",
                KohanaCommandCategory.System,
                _ => ProposeOptimizationAsync(scenario),
                keywords: ["optimizar", "rendimiento", "equipo", "pc", title.ToLowerInvariant()]);
        }

        yield return new KohanaCommandDescriptor(
            "optimize.restaurar",
            "Deshacer la última optimización",
            "Devuelve el equipo al estado guardado antes del último plan aplicado.",
            KohanaCommandCategory.System,
            _ => Task.FromResult(RestoreOptimization()),
            keywords: ["deshacer", "restaurar", "optimizacion", "revertir"],
            availability: () => _optimizationCoordinator.HasSomethingToUndo
                ? KohanaCommandAvailability.Available
                : KohanaCommandAvailability.Unavailable("No hay ninguna optimización aplicada que deshacer."));

        // Diseño D11 — la auditoría es consultable desde el mismo sitio que todo lo demás. Un
        // registro que solo se puede leer abriendo un archivo a mano no lo lee nadie.
        yield return new KohanaCommandDescriptor(
            "optimize.historial",
            "Ver el historial de optimizaciones",
            "Muestra qué se aplicó, qué se deshizo y qué falló, con su fecha.",
            KohanaCommandCategory.System,
            _ =>
            {
                ShowOptimizationAudit();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["historial", "auditoria", "optimizacion", "registro"]);

        // Diseño D13 — el registro completo, no solo el de una capacidad. "¿Qué ha hecho Kohana en
        // mi equipo?" es una pregunta sola, y no debería obligar a saber en qué apartado mirar.
        yield return new KohanaCommandDescriptor(
            "audit.show",
            "Ver todo lo que Kohana ha hecho",
            "El registro completo: qué hizo, cuándo, con qué permiso y cómo deshacerlo.",
            KohanaCommandCategory.System,
            _ =>
            {
                ShowFullAudit();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["auditoria", "registro", "actividad", "historial", "privacidad"]);
    }

    private void RefreshAuditPanel() => _systemView.UpdateAudit(_auditLog.Read());

    /// <summary>
    /// Diseño D18 — deshacer una acción concreta desde el registro. El despacho es por capacidad, y
    /// cada una usa su propio camino de reversión: el registro dice QUÉ se puede deshacer, pero
    /// quien sabe CÓMO sigue siendo la capacidad que lo hizo.
    /// </summary>
    private void RevertAuditEntry(AuditEntry entry)
    {
        if (!entry.CanRevert)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            $"{entry.Detail}{Environment.NewLine}{Environment.NewLine}{entry.RevertHint}" +
                $"{Environment.NewLine}{Environment.NewLine}¿Lo deshago?",
            "Deshacer",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        var detail = entry.Capability switch
        {
            AuditCapability.Optimizacion => _optimizationCoordinator.Restore().Detail,

            AuditCapability.Proyecto when Guid.TryParse(entry.RevertToken, out var checkpointId) =>
                _workspaceEditCoordinator.Revert(checkpointId, _preferences.Workspace).Detail,

            AuditCapability.Permisos when Guid.TryParse(entry.RevertToken, out var snapshotId) =>
                _computerUseCoordinator.Revert(snapshotId, _preferences.ComputerUseAutonomyLevel).Detail,

            // Desactivar un pack toca preferencias con efectos vivos, así que va por su propio
            // camino y no por el genérico: reaplicarlas es parte de deshacerlo.
            AuditCapability.Permisos when Enum.TryParse<SkillPackId>(entry.RevertToken, out _) =>
                RevertSkillPack().Message ?? "Pack desactivado.",

            _ => "Esa acción no sé deshacerla desde aquí."
        };

        _assistantView.AddKohanaMessage(detail);
        ShowFlowNotice(CapsuleKind.Information, "Deshacer", detail);
        RefreshAuditPanel();
    }

    private static string CapabilityTitleFor(KohanaCapability capability) =>
        CapabilityText.Describe(capability);

    /// <summary>
    /// Diseño D16 — el único punto por el que una capacidad pide permiso. Devuelve true si puede
    /// seguir. Cuando el broker pide confirmación, se pregunta aquí y la respuesta queda registrada:
    /// un "sí" que no deja rastro es indistinguible de un permiso que nadie dio.
    /// </summary>
    private bool TryGetPermission(PermissionRequest request)
    {
        var decision = PermissionBroker.Decide(request, _preferences.Permissions);

        if (decision.IsDenied)
        {
            _assistantView.AddKohanaMessage(decision.Reason);
            RecordAudit(
                AuditCapability.Permisos,
                $"Acción denegada ({request.Capability})",
                $"{request.Description} — {decision.Reason}",
                "Permiso denegado");
            return false;
        }

        if (decision.MayProceedWithoutAsking)
        {
            return true;
        }

        var confirmation = MessageBox.Show(
            this,
            $"{request.Description}{Environment.NewLine}{Environment.NewLine}{decision.Reason}" +
                $"{Environment.NewLine}{Environment.NewLine}¿Lo hago?",
            $"Permiso: {CapabilityTitleFor(request.Capability)}",
            MessageBoxButton.YesNo,
            decision.TriggeredCategories.Count > 0
                ? MessageBoxImage.Warning
                : MessageBoxImage.Question);

        var granted = confirmation == MessageBoxResult.Yes;

        RecordAudit(
            AuditCapability.Permisos,
            granted
                ? $"Acción autorizada ({request.Capability})"
                : $"Acción rechazada ({request.Capability})",
            request.Description,
            granted ? "Confirmación explícita del usuario" : "El usuario dijo que no");

        return granted;
    }

    private void ShowFullAudit()
    {
        var entries = _auditLog.Read();

        var message = new StringBuilder();
        if (entries.Count == 0)
        {
            message.Append("Todavía no he hecho nada que valga la pena registrar.");
        }
        else
        {
            message.AppendLine("Todo lo que he hecho, de lo más reciente a lo más antiguo:");
            foreach (var entry in entries.Take(25))
            {
                message.Append("· ").AppendLine(entry.Describe());
            }
        }

        _assistantView.AddKohanaMessage(message.ToString().TrimEnd());
        NavigateTo("Assistant", animate: true);
    }

    /// <summary>
    /// Diseño D13 — conceder o revocar un permiso es de lo primero que debe quedar registrado: es
    /// la decisión de la que cuelgan todas las demás.
    /// </summary>
    private void RecordAudit(
        AuditCapability capability,
        string action,
        string detail,
        string permission,
        int? autonomyLevel = null,
        string revertHint = "")
    {
        _auditLog.Append(new AuditEntry
        {
            At = DateTimeOffset.Now,
            Capability = capability,
            Action = action,
            Detail = detail,
            Permission = permission,
            AutonomyLevel = autonomyLevel,
            RevertHint = revertHint
        });

        RefreshAuditPanel();
    }

    private void ShowOptimizationAudit()
    {
        var entries = _optimizationCoordinator.ReadAudit();

        var message = new StringBuilder();
        if (entries.Count == 0)
        {
            message.Append("Todavía no he aplicado ninguna optimización.");
        }
        else
        {
            message.AppendLine("Historial de optimizaciones:");
            foreach (var entry in entries.Take(15))
            {
                message.Append("· ").AppendLine(entry.Describe());
            }
        }

        _assistantView.AddKohanaMessage(message.ToString().TrimEnd());
        NavigateTo("Assistant", animate: true);
    }

    private async Task<CommandExecutionResult> ProposeOptimizationAsync(OptimizationScenario scenario)
    {
        var profile = await _hardwareCapabilityService.RefreshAsync(_lifetimeCancellation.Token);
        var plan = OptimizationPlanBuilder.Build(scenario, profile);

        ShowOptimizationPlan(plan);
        return CommandExecutionResult.Success();
    }

    /// <summary>
    /// Diseño D8 — enseña el plan y, solo si hay algo que Kohana pueda aplicar Y revertir, ofrece
    /// aplicarlo. La confirmación es un diálogo explícito: el modelo de confianza no permite pasar
    /// de proponer a actuar sin que la persona lo diga.
    /// </summary>
    private void ShowOptimizationPlan(OptimizationPlan plan)
    {
        var message = new StringBuilder();
        message.AppendLine(plan.Summary);

        foreach (var change in plan.Changes)
        {
            message.AppendLine();
            message.Append(change.Target == OptimizationTarget.Advice ? "· Consejo: " : "· Cambio: ");
            message.AppendLine(change.Title);
            message.Append("  ").AppendLine(change.Justification);
        }

        foreach (var skipped in plan.SkippedForMissingData)
        {
            message.AppendLine();
            message.Append("· No propuesto: ").AppendLine(skipped);
        }

        _assistantView.AddKohanaMessage(message.ToString().TrimEnd());

        if (!plan.RequiresSnapshot)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            $"{plan.Summary}" + Environment.NewLine + Environment.NewLine +
                "¿Aplico los cambios? Guardaré cómo está ahora para poder deshacerlo.",
            "Optimizar equipo",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        ApplyOptimization(plan);
    }

    /// <summary>
    /// Diseño D11 — el orden (snapshot antes que nada, reversión de lo ya aplicado si un paso falla)
    /// vive ahora en <see cref="OptimizationCoordinator"/>, en Core, donde se puede probar con
    /// dobles. Aquí solo queda enseñar el resultado.
    /// </summary>
    private void ApplyOptimization(OptimizationPlan plan)
    {
        var result = _optimizationCoordinator.Apply(plan);

        ShowFlowNotice(
            result.IsApplied ? CapsuleKind.Success : CapsuleKind.Warning,
            result.IsApplied ? "Equipo optimizado" : "No se pudo optimizar",
            result.Detail);

        _systemView.SetOptimizationStatus(result.Detail, _optimizationCoordinator.HasSomethingToUndo);
        RefreshAuditPanel();
    }

    private CommandExecutionResult RestoreOptimization()
    {
        var result = _optimizationCoordinator.Restore();

        _systemView.SetOptimizationStatus(result.Detail, _optimizationCoordinator.HasSomethingToUndo);
        RefreshAuditPanel();

        if (!result.IsApplied)
        {
            return CommandExecutionResult.Failure(result.Detail);
        }

        ShowFlowNotice(CapsuleKind.Success, "Optimización deshecha", result.Detail);
        return CommandExecutionResult.Success(result.Detail);
    }

    /// <summary>
    /// Diseño D9 (Fase 6) — comandos de memoria. "Ver" y "olvidar todo" existen aunque la memoria
    /// esté apagada: revocar y auditar deben funcionar SIEMPRE. Si apagar la memoria bloqueara el
    /// borrado, lo ya guardado quedaría atrapado justo cuando la persona quiere deshacerse de ello.
    /// </summary>
    private IEnumerable<KohanaCommandDescriptor> BuildMemoryCommands()
    {
        yield return new KohanaCommandDescriptor(
            "memory.show",
            "Ver lo que Kohana recuerda",
            "Muestra, en texto claro, todo lo que hay guardado en la memoria.",
            KohanaCommandCategory.System,
            _ =>
            {
                ShowMemoryContents();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["memoria", "recuerda", "guardado", "privacidad"]);

        yield return new KohanaCommandDescriptor(
            "memory.forgetAll",
            "Olvidar todo lo que Kohana recuerda",
            "Borra por completo la memoria guardada. No se puede deshacer.",
            KohanaCommandCategory.System,
            _ => Task.FromResult(ForgetAllMemory()),
            keywords: ["olvidar", "borrar", "memoria", "privacidad"]);
    }

    /// <summary>
    /// Diseño D10 (Fase 6) — la memoria ya se llena desde la conversación, que es lo que D9 dejó
    /// pendiente. Dos caminos con reglas distintas a propósito:
    ///
    /// - **Explícito** ("recuerda que ..."): se guarda. La persona acaba de dar la orden; volver a
    ///   preguntarle "¿seguro?" sería ruido. Si la política lo rechaza, se dice POR QUÉ — un
    ///   "recuerda que ..." que no guarda nada y no explica nada parecería que funcionó.
    /// - **Observado** (una preferencia dicha de paso): **nunca** se guarda solo. Se propone y hace
    ///   falta un sí. Guardar lo que alguien mencionó sin pedirlo es exactamente la vigilancia
    ///   silenciosa que el roadmap prohíbe para esta fase.
    ///
    /// Devuelve true solo cuando la frase ERA la orden de memoria y ya está atendida.
    /// </summary>
    private bool TryHandleMemoryPrompt(string prompt)
    {
        if (_pendingMemoryCandidate is { } pending)
        {
            // Una sola oportunidad: la propuesta caduca con la siguiente frase, sea cual sea. Una
            // pregunta que sigue viva varios turnos acabaría capturando un "sí" dicho por otra cosa.
            _pendingMemoryCandidate = null;

            if (IsVoiceConfirmation(SpanishVoiceTranscriptNormalizer.Normalize(prompt)))
            {
                SaveMemoryCandidate(pending);
                return true;
            }
        }

        var candidate = MemoryCandidateDetector.Detect(prompt);
        if (candidate is null)
        {
            return false;
        }

        if (candidate.Source == MemoryCandidateSource.Explicito)
        {
            SaveMemoryCandidate(candidate);
            return true;
        }

        // Se consulta la política ANTES de proponer: preguntar "¿lo recuerdo?" para después
        // rechazarlo por una exclusión propia sería hacer perder el tiempo a la persona.
        var verdict = MemoryPolicy.CanRemember(candidate.Category, candidate.Text, _preferences.Memory);
        if (!verdict.Success)
        {
            // En silencio: nadie pidió recordar nada, así que un aviso aquí sería una interrupción
            // no solicitada.
            return false;
        }

        _pendingMemoryCandidate = candidate;
        ShowFlowNotice(
            CapsuleKind.Information,
            "¿Lo recuerdo?",
            $"«{candidate.Text}». Responde «sí» para guardarlo.");

        // La frase sigue su curso normal: la propuesta es aparte, no reemplaza la conversación.
        return false;
    }

    private void SaveMemoryCandidate(MemoryCandidate candidate)
    {
        var result = _memoryManager.Remember(
            candidate.Category,
            candidate.Text,
            _preferences.Memory,
            DateTimeOffset.Now);

        _assistantView.AddKohanaMessage(result.Success
            ? $"{result.Message} «{candidate.Text}»"
            : result.Message);

        ShowFlowNotice(
            result.Success ? CapsuleKind.Success : CapsuleKind.Warning,
            result.Success ? "Lo recordaré" : "No lo recordé",
            result.Message);
    }

    private void ShowMemoryContents()
    {
        var settings = _preferences.Memory;
        var entries = _memoryManager.GetAll(settings, DateTimeOffset.Now);

        var message = new StringBuilder();
        message.AppendLine(settings.Enabled
            ? $"Memoria activada · retención de {settings.RetentionDays} días."
            : "La memoria está desactivada: no estoy guardando nada nuevo.");

        if (entries.Count == 0)
        {
            message.Append("No hay nada guardado.");
        }
        else
        {
            foreach (var entry in entries)
            {
                message.AppendLine();
                message.Append("· [")
                    .Append(MemoryPolicy.CategoryLabel(entry.Category))
                    .Append("] ")
                    .Append(entry.Text);
            }
        }

        if (settings.Exclusions.Count > 0)
        {
            message.AppendLine();
            message.Append("Exclusiones activas: ").Append(string.Join(", ", settings.Exclusions));
        }

        _assistantView.AddKohanaMessage(message.ToString().TrimEnd());
    }

    private CommandExecutionResult ForgetAllMemory()
    {
        // Diseño D16 — pasa por el broker en vez de por un diálogo propio. Borrar sin recuperación
        // es una de las siete categorías de confirmación obligatoria, así que preguntará aunque la
        // memoria esté permitida: es exactamente lo que el broker existe para garantizar.
        if (!TryGetPermission(new PermissionRequest(
                KohanaCapability.Memoria,
                "Borrar todo lo que Kohana recuerda. No se puede deshacer.",
                Categories: [MandatoryConfirmation.BorradoIrreversible])))
        {
            return CommandExecutionResult.Failure("No borré nada.");
        }

        var result = _memoryManager.ForgetEverything();

        RecordAudit(
            AuditCapability.Memoria,
            "Memoria borrada por completo",
            result.Message,
            "Confirmación explícita del usuario");

        ShowFlowNotice(CapsuleKind.Success, "Memoria borrada", result.Message);
        return CommandExecutionResult.Success(result.Message);
    }

    /// <summary>
    /// Diseño D12 (Fase 5 — Project Companion) — cuatro comandos y ni uno que escriba. La capacidad
    /// vive en los niveles 1–3 del modelo de confianza ("ninguna capacidad nueva puede empezar en el
    /// nivel 6"), así que Kohana lee, explica y guía; los cambios los aplica la persona.
    ///
    /// Autorizar y revocar están al mismo nivel, en el mismo sitio: un permiso que cuesta más
    /// quitar que dar no es un permiso, es una trampa.
    /// </summary>
    private IEnumerable<KohanaCommandDescriptor> BuildWorkspaceCommands()
    {
        yield return new KohanaCommandDescriptor(
            "workspace.authorize",
            "Autorizar una carpeta de proyecto",
            "Elige la carpeta que Kohana podrá leer. Solo lectura: no modifica archivos.",
            KohanaCommandCategory.System,
            _ =>
            {
                AuthorizeWorkspaceFolder();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["proyecto", "carpeta", "workspace", "autorizar", "codigo"]);

        yield return new KohanaCommandDescriptor(
            "workspace.show",
            "Ver el proyecto autorizado",
            "Muestra qué carpeta puede leer Kohana y con qué nivel de autonomía.",
            KohanaCommandCategory.System,
            _ =>
            {
                ShowWorkspaceStatus();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["proyecto", "workspace", "permisos", "acceso"]);

        yield return new KohanaCommandDescriptor(
            "workspace.explain",
            "Explicar el proyecto autorizado",
            "Kohana revisa la estructura del proyecto y te la explica. No cambia nada.",
            KohanaCommandCategory.System,
            async _ => await ExplainWorkspaceAsync(),
            keywords: ["proyecto", "explicar", "estructura", "codigo"],
            availability: () => _preferences.Workspace.HasAuthorizedFolder
                ? KohanaCommandAvailability.Available
                : KohanaCommandAvailability.Unavailable("Todavía no autorizaste ninguna carpeta de proyecto."));

        // Diseño D13 — la búsqueda existía desde D12 pero sin comando que la expusiera. Devuelve
        // ruta, línea y la línea encontrada, ya redactada: una coincidencia puede caer justo encima
        // de un token.
        yield return new KohanaCommandDescriptor(
            "workspace.search",
            "Buscar en el proyecto autorizado",
            "Busca un texto dentro de los archivos que Kohana puede leer.",
            KohanaCommandCategory.System,
            _ =>
            {
                SearchWorkspace();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["buscar", "proyecto", "codigo", "workspace"],
            availability: () => _preferences.Workspace.HasAuthorizedFolder
                ? KohanaCommandAvailability.Available
                : KohanaCommandAvailability.Unavailable("Todavía no autorizaste ninguna carpeta de proyecto."));

        // Diseño D14 (Fase 5, nivel 4) — proponer y aplicar UN cambio. Solo aparece cuando el nivel
        // de autonomía lo permite: un comando visible que siempre responde "no puedo" enseña a
        // ignorar los mensajes de permisos.
        yield return new KohanaCommandDescriptor(
            "workspace.edit",
            "Pedir un cambio en el proyecto",
            "Kohana propone el cambio, te lo enseña y solo lo aplica si lo confirmas. Siempre se puede deshacer.",
            KohanaCommandCategory.System,
            _ =>
            {
                RequestWorkspaceEdit();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["cambiar", "editar", "proyecto", "codigo", "modificar"],
            availability: () => !_preferences.Workspace.HasAuthorizedFolder
                ? KohanaCommandAvailability.Unavailable("Todavía no autorizaste ninguna carpeta de proyecto.")
                : WorkspaceAutonomyPolicy.CanWrite(_preferences.Workspace.AutonomyLevel)
                    ? KohanaCommandAvailability.Available
                    : KohanaCommandAvailability.Unavailable(
                        "Sube el nivel de autonomía del proyecto a «Ejecutar un paso» en Personalizar."));

        yield return new KohanaCommandDescriptor(
            "workspace.undo",
            "Deshacer el último cambio en el proyecto",
            "Devuelve el archivo a como estaba antes de que Kohana lo tocara.",
            KohanaCommandCategory.System,
            _ => Task.FromResult(UndoLastWorkspaceEdit()),
            keywords: ["deshacer", "revertir", "proyecto", "cambio"],
            availability: () => _workspaceEditCoordinator.Checkpoints.Count > 0
                ? KohanaCommandAvailability.Available
                : KohanaCommandAvailability.Unavailable("No he cambiado nada en tu proyecto."));

        yield return new KohanaCommandDescriptor(
            "workspace.revoke",
            "Revocar el acceso al proyecto",
            "Kohana deja de poder leer esa carpeta, en el acto.",
            KohanaCommandCategory.System,
            _ => Task.FromResult(RevokeWorkspace()),
            keywords: ["revocar", "quitar", "proyecto", "acceso", "privacidad"],
            availability: () => _preferences.Workspace.HasAuthorizedFolder
                ? KohanaCommandAvailability.Available
                : KohanaCommandAvailability.Unavailable("No hay ninguna carpeta autorizada."));
    }

    private void AuthorizeWorkspaceFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Elige la carpeta del proyecto que Kohana podrá leer",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var chosen = dialog.FolderName;

        // La confirmación dice qué se concede Y qué no. Un permiso que solo enumera lo que gana
        // quien lo pide no deja decidir a quien lo da.
        var confirmation = MessageBox.Show(
            this,
            $"Kohana podrá LEER los archivos de:{Environment.NewLine}{chosen}{Environment.NewLine}{Environment.NewLine}" +
                "No podrá modificarlos ni borrarlos. No leerá .env, claves ni carpetas de dependencias, " +
                "y ocultará los valores que parezcan secretos antes de enviar nada a la IA." +
                $"{Environment.NewLine}{Environment.NewLine}Puedes revocarlo cuando quieras. ¿Lo autorizo?",
            "Autorizar carpeta de proyecto",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        _preferences.Workspace.AuthorizedPath = chosen;
        _preferences.Workspace.AuthorizedAt = DateTimeOffset.Now;
        _preferences.Workspace.Normalize();
        SavePreferences();

        RecordAudit(
            AuditCapability.Permisos,
            "Carpeta de proyecto autorizada",
            chosen,
            "Autorización explícita del usuario",
            (int)_preferences.Workspace.AutonomyLevel,
            "Revocar el acceso al proyecto.");

        ShowFlowNotice(
            CapsuleKind.Success,
            "Proyecto autorizado",
            $"Puedo leer {Path.GetFileName(Path.TrimEndingDirectorySeparator(chosen))}. Solo lectura.");
        ShowWorkspaceStatus();
    }

    /// <summary>
    /// Diseño D13 — la paleta de comandos no acepta argumentos, así que la consulta se pide en la
    /// conversación: el comando deja a Kohana esperando UNA frase, igual que la propuesta de
    /// recuerdo de D10. Vale una sola, y se cancela como cualquier otra pregunta.
    /// </summary>
    private void SearchWorkspace()
    {
        _awaitingWorkspaceSearchQuery = true;
        _assistantView.AddKohanaMessage("¿Qué busco en el proyecto? Escríbelo y lo busco.");
        NavigateTo("Assistant", animate: true);
    }

    private bool TryHandleWorkspaceSearchPrompt(string prompt)
    {
        if (!_awaitingWorkspaceSearchQuery)
        {
            return false;
        }

        _awaitingWorkspaceSearchQuery = false;

        var normalized = SpanishVoiceTranscriptNormalizer.Normalize(prompt);
        if (IsVoiceCancellation(normalized))
        {
            _assistantView.AddKohanaMessage("De acuerdo, no busco nada.");
            return true;
        }

        var workspace = _preferences.Workspace;
        if (!workspace.HasAuthorizedFolder)
        {
            // El acceso pudo revocarse entre el comando y la respuesta. Revocar tiene que surtir
            // efecto en el acto, incluso a media conversación.
            _assistantView.AddKohanaMessage("Ya no tengo ninguna carpeta autorizada, así que no busqué nada.");
            return true;
        }

        var hits = _workspaceReader.Search(workspace.AuthorizedPath, prompt, maximumHits: 40);

        var message = new StringBuilder();
        if (hits.Count == 0)
        {
            message.Append($"No encontré «{prompt.Trim()}» en el proyecto.");
        }
        else
        {
            message.AppendLine($"{hits.Count} coincidencias de «{prompt.Trim()}»:");
            foreach (var hit in hits.Take(25))
            {
                message.Append("· ")
                    .Append(hit.RelativePath)
                    .Append(':')
                    .Append(hit.LineNumber)
                    .Append("  ")
                    .AppendLine(hit.Line);
            }
        }

        _assistantView.AddKohanaMessage(message.ToString().TrimEnd());
        return true;
    }

    /// <summary>
    /// Diseño D17 (Fase 7 — Safe Computer Use) — Kohana dice **qué haría y por qué método**, sin
    /// hacerlo. El roadmap prohíbe saltarse niveles del modelo de autonomía, y esta capacidad acaba
    /// de nacer: empieza donde tienen que empezar todas.
    /// </summary>
    private IEnumerable<KohanaCommandDescriptor> BuildComputerUseCommands()
    {
        yield return new KohanaCommandDescriptor(
            "computeruse.plan",
            "Proponer cómo hacer algo en el equipo",
            "Kohana elige la forma más segura de hacerlo y te la explica. No lo ejecuta.",
            KohanaCommandCategory.System,
            _ =>
            {
                _awaitingComputerUseIntent = true;
                _assistantView.AddKohanaMessage(
                    "¿Qué quieres hacer en el equipo? Te digo cómo lo haría y por qué de esa forma.");
                NavigateTo("Assistant", animate: true);
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["equipo", "hacer", "accion", "automatizar", "computer use"]);

        yield return new KohanaCommandDescriptor(
            "computeruse.methods",
            "Ver cómo puede actuar Kohana en el equipo",
            "Muestra los métodos disponibles, en orden de más a menos seguro.",
            KohanaCommandCategory.System,
            _ =>
            {
                ShowComputerUseMethods();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["metodos", "seguridad", "equipo", "computer use"]);

        // Diseño D18 (Fase 7, nivel 4) — los comandos de la lista de permitidos, uno por uno. Solo
        // aparecen habilitados cuando el permiso y el nivel lo consienten: un comando visible que
        // siempre responde "no puedo" enseña a ignorar los mensajes de permisos.
        foreach (var command in SafeShellCatalog.All)
        {
            var current = command;

            yield return new KohanaCommandDescriptor(
                $"computeruse.run.{current.Id}",
                current.Title,
                $"Ejecuta «{current.Executable} {current.Arguments}». Solo lee: no cambia nada del equipo.",
                KohanaCommandCategory.System,
                _ =>
                {
                    RunSafeCommand(current);
                    return Task.FromResult(CommandExecutionResult.Success());
                },
                keywords: ["equipo", "diagnostico", current.Title.ToLowerInvariant()],
                availability: ComputerUseAvailability);
        }
    }

    /// <summary>
    /// Diseño D20 (Fase 9 — Productization) — actualizar y desinstalar sin sorpresas. Nada de esto
    /// actualiza ni desinstala: prepara la copia verificada de la que volver, y enseña qué se lleva
    /// y qué se queda.
    /// </summary>
    private IEnumerable<KohanaCommandDescriptor> BuildProductizationCommands()
    {
        yield return new KohanaCommandDescriptor(
            "data.inventory",
            "Ver qué guarda Kohana en tu equipo",
            "La lista completa: qué es cada archivo, si contiene datos tuyos y si está cifrado.",
            KohanaCommandCategory.System,
            _ =>
            {
                ShowDataInventory();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["datos", "privacidad", "guardar", "archivos"]);

        yield return new KohanaCommandDescriptor(
            "update.prepare",
            "Preparar una copia antes de actualizar",
            "Copia tus datos y comprueba que la copia está bien, para poder volver si algo sale mal.",
            KohanaCommandCategory.System,
            _ =>
            {
                PrepareUpdateBackup();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["actualizar", "copia", "seguridad", "respaldo"]);

        yield return new KohanaCommandDescriptor(
            "update.rollback",
            "Restaurar la última copia de tus datos",
            "Devuelve tus ajustes, tareas y demás a como estaban en la copia más reciente.",
            KohanaCommandCategory.System,
            _ =>
            {
                RestoreLatestBackup();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["restaurar", "copia", "volver", "respaldo"],
            availability: () => _backupService.ListBackups().Count > 0
                ? KohanaCommandAvailability.Available
                : KohanaCommandAvailability.Unavailable("Todavía no hay ninguna copia guardada."));

        // Diseño D21 — el criterio de terminado de la Fase 9 pide "diagnóstico exportable para
        // soporte". Exportar es enviar, así que el archivo se redacta y dice qué dejó fuera.
        yield return new KohanaCommandDescriptor(
            "support.export",
            "Exportar un diagnóstico para soporte",
            "Guarda un archivo con versiones, estado y actividad reciente. Sin tus datos, y te dice qué dejó fuera.",
            KohanaCommandCategory.System,
            _ =>
            {
                ExportSupportBundle();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["soporte", "diagnostico", "exportar", "ayuda", "problema"]);

        yield return new KohanaCommandDescriptor(
            "privacy.report",
            "Ver el informe de privacidad",
            "Qué guarda Kohana, dónde, si está cifrado y cómo borrarlo.",
            KohanaCommandCategory.System,
            _ =>
            {
                ShowPrivacyReport();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["privacidad", "datos", "cifrado", "borrar"]);

        yield return new KohanaCommandDescriptor(
            "uninstall.plan",
            "Ver qué pasaría al desinstalar Kohana",
            "Enseña qué se borraría y qué se conservaría, antes de desinstalar nada.",
            KohanaCommandCategory.System,
            _ =>
            {
                ShowUninstallPlan();
                return Task.FromResult(CommandExecutionResult.Success());
            },
            keywords: ["desinstalar", "borrar", "quitar", "datos"]);
    }

    private void ShowDataInventory()
    {
        var message = new StringBuilder();
        message.AppendLine($"Todo lo que guardo está en {NexoDataPaths.RootDirectory}:");

        foreach (var item in KohanaDataInventory.All)
        {
            var exists = File.Exists(item.FullPath);
            message.AppendLine();
            message.Append("· ").Append(item.Title)
                .Append(exists ? "" : " (todavía no existe)")
                .AppendLine();
            message.Append("  ").AppendLine(item.WhatItHolds);
            message.Append("  ")
                .Append(item.IsPersonal ? "Contiene datos tuyos." : "No contiene datos personales.")
                .AppendLine(item.IsEncrypted ? " Cifrado." : string.Empty);
        }

        _assistantView.AddKohanaMessage(message.ToString().TrimEnd());
        NavigateTo("Assistant", animate: true);
    }

    private void PrepareUpdateBackup()
    {
        var readiness = _updateSafety.PrepareUpdate();

        var message = new StringBuilder();
        message.AppendLine(readiness.Detail);

        if (readiness.Backup is { } backup)
        {
            foreach (var file in backup.Files.Where(file => file.Copied))
            {
                message.Append("· ").Append(file.FileName).Append(" — ").AppendLine(file.Detail);
            }
        }

        _assistantView.AddKohanaMessage(message.ToString().TrimEnd());
        NavigateTo("Assistant", animate: true);

        ShowFlowNotice(
            readiness.CanUpdate ? CapsuleKind.Success : CapsuleKind.Warning,
            readiness.CanUpdate ? "Copia verificada" : "Copia incompleta",
            readiness.Detail);

        RefreshAuditPanel();
    }

    private void RestoreLatestBackup()
    {
        var latest = _backupService.ListBackups().FirstOrDefault();
        if (latest is null)
        {
            _assistantView.AddKohanaMessage("Todavía no hay ninguna copia guardada.");
            return;
        }

        // Restaurar sobrescribe lo que tengas ahora: es de las cosas que el modelo de confianza
        // manda confirmar siempre, tenga el permiso que tenga.
        if (!TryGetPermission(new PermissionRequest(
                KohanaCapability.Memoria,
                $"Restaurar la copia «{latest}». Sobrescribe tus ajustes, tareas y memoria actuales.",
                Categories: [MandatoryConfirmation.BorradoIrreversible])))
        {
            return;
        }

        var result = _updateSafety.Rollback(latest);

        _assistantView.AddKohanaMessage(result.Detail);
        ShowFlowNotice(
            result.Success ? CapsuleKind.Success : CapsuleKind.Warning,
            result.Success ? "Datos restaurados" : "No se pudo restaurar",
            result.Detail);

        if (result.Success)
        {
            _assistantView.AddKohanaMessage(
                "Reinicia Kohana para que use los datos restaurados.");
        }

        RefreshAuditPanel();
    }

    /// <summary>
    /// Diseño D21 — genera el paquete de soporte y deja que la persona elija dónde guardarlo.
    /// Exportar es enviar: el contenido va redactado y el archivo dice qué dejó fuera, para que se
    /// pueda revisar antes de mandárselo a nadie.
    /// </summary>
    private async void ExportSupportBundle()
    {
        NexoDiagnosticSnapshot snapshot;
        try
        {
            snapshot = await _diagnosticService.CaptureAsync(
                _preferences,
                _voiceCoordinator.GetInputDevices(),
                _voiceCoordinator.IsVoiceInputReady,
                _voiceCoordinator.IsWakeWordReady,
                _voiceCoordinator.IsWakeWordListening,
                trayActive: true,
                _startupService.IsEnabled(),
                _lifetimeCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var bundle = SupportBundleBuilder.Build(snapshot, _auditLog.Read(), File.Exists);

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Guardar el diagnóstico para soporte",
            FileName = $"kohana-soporte-{DateTimeOffset.Now:yyyyMMdd-HHmm}.txt",
            Filter = "Archivo de texto (*.txt)|*.txt",
            DefaultExt = ".txt"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, bundle);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            ShowFlowNotice(CapsuleKind.Warning, "No pude guardarlo", exception.Message);
            return;
        }

        RecordAudit(
            AuditCapability.Permisos,
            "Diagnóstico exportado",
            PathRedactor.Shorten(dialog.FileName),
            "Exportación pedida por el usuario");

        _assistantView.AddKohanaMessage(
            $"Guardé el diagnóstico en {dialog.FileName}." + Environment.NewLine +
            "Ábrelo y léelo entero antes de mandárselo a nadie: al final dice qué dejé fuera.");
        NavigateTo("Assistant", animate: true);

        ShowFlowNotice(CapsuleKind.Success, "Diagnóstico guardado", "Sin tus datos personales dentro.");
    }

    private void ShowPrivacyReport()
    {
        var report = PrivacyReportBuilder.Build(
            NexoDataPaths.RootDirectory,
            File.Exists,
            path =>
            {
                try
                {
                    return new FileInfo(path).Length;
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException)
                {
                    return 0;
                }
            });

        _assistantView.AddKohanaMessage(report);
        NavigateTo("Assistant", animate: true);
    }

    private void ShowUninstallPlan()
    {
        var keep = UninstallPlanner.Build(UninstallDataChoice.Conservar);

        var message = new StringBuilder();
        message.AppendLine("Si desinstalas Kohana conservando tus datos:");
        message.AppendLine();
        message.AppendLine(UninstallPlanner.Describe(keep));
        message.AppendLine();
        message.AppendLine(
            "Si prefieres que no quede nada, borra la carpeta de datos a mano después de " +
            $"desinstalar: {NexoDataPaths.RootDirectory}");

        _assistantView.AddKohanaMessage(message.ToString().TrimEnd());
        NavigateTo("Assistant", animate: true);
    }

    private KohanaCommandAvailability ComputerUseAvailability()
    {
        if (_preferences.Permissions.For(KohanaCapability.ComputerUse).Level == PermissionLevel.Bloqueado)
        {
            return KohanaCommandAvailability.Unavailable(
                "Actuar sobre el equipo está bloqueado. Puedes cambiarlo en Personalizar.");
        }

        return ComputerUseAutonomyPolicy.CanExecute(_preferences.ComputerUseAutonomyLevel)
            ? KohanaCommandAvailability.Available
            : KohanaCommandAvailability.Unavailable(
                "Sube el nivel de autonomía a «Ejecutar un paso» en Personalizar.");
    }

    /// <summary>
    /// Diseño D18 — ejecuta un comando de la lista. La confirmación la pide el broker; el
    /// coordinador comprueba permiso, nivel y método antes de lanzar nada.
    /// </summary>
    private void RunSafeCommand(SafeShellCommand command)
    {
        if (!TryGetPermission(new PermissionRequest(
                KohanaCapability.ComputerUse,
                $"{command.Title} ({command.Executable} {command.Arguments})")))
        {
            return;
        }

        var result = _computerUseCoordinator.Execute(
            new ComputerUseRequest(
                ComputerUseMethod.ShellSeguro,
                command.Title,
                SafeCommandId: command.Id),
            _preferences.Permissions,
            _preferences.ComputerUseAutonomyLevel);

        var message = new StringBuilder();
        message.AppendLine(result.Detail);

        if (!string.IsNullOrWhiteSpace(result.Output))
        {
            message.AppendLine();
            message.Append(result.Output.Trim());
        }

        _assistantView.AddKohanaMessage(message.ToString().TrimEnd());
        NavigateTo("Assistant", animate: true);
        RefreshAuditPanel();
    }

    private void ShowComputerUseMethods()
    {
        var available = _computerUseProbe.GetAvailableMethods(targetApp: null);

        var message = new StringBuilder();
        message.AppendLine(
            "Cuando actúo sobre el equipo, siempre elijo la forma más segura disponible, en este " +
            "orden. Ratón y teclado simulados van los últimos a propósito: no distinguen ventanas " +
            "ni pueden comprobar qué hicieron.");
        message.AppendLine();

        foreach (var method in Enum.GetValues<ComputerUseMethod>().OrderBy(method => (int)method))
        {
            message.Append((int)method).Append(". ")
                .Append(ComputerUseMethodText.Describe(method))
                .Append(available.Contains(method) ? " — disponible" : " — todavía no implementado")
                .AppendLine();
        }

        _assistantView.AddKohanaMessage(message.ToString().TrimEnd());
        NavigateTo("Assistant", animate: true);
    }

    /// <summary>
    /// Diseño D17 — arma el plan y lo enseña. Si algo lo bloquea, se dice **qué** lo bloquea: un
    /// plan que no llega a formarse deja a la persona sin saber qué haría falta para que sí.
    /// </summary>
    private bool TryHandleComputerUseIntent(string prompt)
    {
        if (!_awaitingComputerUseIntent)
        {
            return false;
        }

        _awaitingComputerUseIntent = false;

        if (IsVoiceCancellation(SpanishVoiceTranscriptNormalizer.Normalize(prompt)))
        {
            _assistantView.AddKohanaMessage("De acuerdo, no propongo nada.");
            return true;
        }

        var intent = new ComputerUseIntent(prompt.Trim());
        var plan = ComputerUsePlanner.Build(
            intent,
            _computerUseProbe.GetAvailableMethods(intent.TargetApp),
            _preferences.Permissions,
            _preferences.ComputerUseAutonomyLevel);

        var message = new StringBuilder();
        message.Append("Lo que pides: ").AppendLine(intent.Description);
        message.AppendLine();
        message.Append("Cómo lo haría: ").AppendLine(plan.Choice.Reason);

        foreach (var rejection in plan.Choice.Rejected)
        {
            message.Append("· Descarté ")
                .Append(ComputerUseMethodText.Describe(rejection.Method))
                .Append(": ").AppendLine(rejection.Reason);
        }

        message.Append("Vuelta atrás: ").AppendLine(plan.ReversalNote);

        if (plan.Blocker is not null)
        {
            message.AppendLine();
            message.Append("Ahora mismo no puedo hacerlo: ").AppendLine(plan.Blocker);
        }

        _assistantView.AddKohanaMessage(message.ToString().TrimEnd());

        RecordAudit(
            AuditCapability.Permisos,
            "Plan de acción propuesto",
            $"{intent.Description} — {plan.Choice.Reason}",
            "Solo propuesta: no se ejecutó nada",
            (int)plan.Level);

        return true;
    }

    /// <summary>
    /// Diseño D15 (Fase 8 — Skills Platform) — activar un pack deja configuradas de una vez varias
    /// capacidades que ya existen. Un pack **nunca concede un permiso**: si le falta alguno, lo dice
    /// y explica dónde se da.
    /// </summary>
    private IEnumerable<KohanaCommandDescriptor> BuildSkillPackCommands()
    {
        foreach (var pack in SkillPackCatalog.All)
        {
            var current = pack;

            yield return new KohanaCommandDescriptor(
                $"skills.{current.Id}".ToLowerInvariant(),
                $"Activar {current.Name}",
                current.Purpose,
                KohanaCommandCategory.System,
                _ =>
                {
                    ApplySkillPack(current);
                    return Task.FromResult(CommandExecutionResult.Success());
                },
                keywords: ["pack", "modo", "skills", current.Name.ToLowerInvariant()]);
        }

        yield return new KohanaCommandDescriptor(
            "skills.off",
            "Desactivar el pack activo",
            "Devuelve los ajustes a como estaban antes de activarlo.",
            KohanaCommandCategory.System,
            _ => Task.FromResult(RevertSkillPack()),
            keywords: ["pack", "desactivar", "quitar", "skills"],
            availability: () => _skillPackCoordinator.ActivePackId is not null
                ? KohanaCommandAvailability.Available
                : KohanaCommandAvailability.Unavailable("No hay ningún pack activo."));
    }

    private void ApplySkillPack(SkillPack pack)
    {
        var plan = _skillPackCoordinator.Preview(pack, _preferences);

        var message = new StringBuilder();
        message.AppendLine(pack.Purpose);

        if (plan.Changes.Count == 0)
        {
            message.AppendLine().Append("Ya está todo como lo dejaría este pack.");
        }
        else
        {
            message.AppendLine();
            message.AppendLine("Cambiaría estos ajustes:");
            foreach (var change in plan.Changes)
            {
                message.Append("· ").Append(change.Title)
                    .Append(" (ahora: ").Append(change.Current).AppendLine(")");
            }
        }

        // Lo que el pack NO puede hacer se enseña con el mismo peso que lo que sí: enseñar solo lo
        // que gana la persona sería vender el pack, no explicarlo.
        if (plan.UnmetRequirements.Count > 0)
        {
            message.AppendLine();
            message.AppendLine("Esto le haría falta y NO lo activo yo, lo decides tú:");
            foreach (var requirement in plan.UnmetRequirements)
            {
                message.Append("· ").Append(requirement.Title).Append(" — ")
                    .Append(requirement.WhyItMatters).Append(' ')
                    .AppendLine(requirement.HowToEnable);
            }
        }

        _assistantView.AddKohanaMessage(message.ToString().TrimEnd());
        NavigateTo("Assistant", animate: true);

        if (!plan.HasSomethingToDo)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            this,
            $"Voy a cambiar {plan.Changes.Count} ajustes para dejar {pack.Name} listo." +
                Environment.NewLine + Environment.NewLine +
                "No activo ningún permiso: la memoria, Vision y la carpeta de proyecto siguen como " +
                "las tengas." + Environment.NewLine + Environment.NewLine +
                "Puedes desactivarlo cuando quieras y todo vuelve a como estaba. ¿Lo activo?",
            $"Activar {pack.Name}",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        var result = _skillPackCoordinator.Apply(pack, _preferences);
        AfterSkillPackChange(result, result.Success ? "Pack activado" : "No activé el pack");
    }

    private CommandExecutionResult RevertSkillPack()
    {
        var result = _skillPackCoordinator.Revert(_preferences);
        AfterSkillPackChange(result, result.Success ? "Pack desactivado" : "No había pack activo");

        return result.Success
            ? CommandExecutionResult.Success(result.Detail)
            : CommandExecutionResult.Failure(result.Detail);
    }

    /// <summary>
    /// Diseño D15 — un pack toca preferencias que ya tienen efectos vivos (el atajo de dictado, el
    /// plan de motores, los módulos visibles). Se reaplican todas en vez de una lista a mano: una
    /// lista se queda corta en cuanto un pack cambia un ajuste nuevo.
    /// </summary>
    private void AfterSkillPackChange(SkillPackResult result, string title)
    {
        SavePreferences();
        ApplyPreferences();
        _settingsView.ApplyPreferences(_preferences);
        ApplyFlowHotkeyRegistration();
        RefreshAdaptiveEnginePlan();
        RefreshAuditPanel();

        _assistantView.AddKohanaMessage(result.Detail);
        ShowFlowNotice(result.Success ? CapsuleKind.Success : CapsuleKind.Warning, title, result.Detail);
    }

    // ---------- Diseño D14 (Fase 5, nivel 4 — "Ejecutar un paso") ----------

    private void RequestWorkspaceEdit()
    {
        _awaitingWorkspaceEditInstruction = true;
        _assistantView.AddKohanaMessage(
            "¿Qué quieres que cambie? Dímelo y te enseño el cambio antes de tocar nada.");
        NavigateTo("Assistant", animate: true);
    }

    /// <summary>
    /// Diseño D14 — la instrucción va a la IA con el proyecto como contexto y con el formato de
    /// cambio exigido. La respuesta se parsea; si no cumple el formato exacto se enseña como texto y
    /// no se escribe nada. Adivinar lo que el modelo quiso decir es como se acaba escribiendo en el
    /// archivo equivocado.
    /// </summary>
    private async Task<bool> TryHandleWorkspaceEditInstructionAsync(string prompt, bool fromVoice)
    {
        if (!_awaitingWorkspaceEditInstruction)
        {
            return false;
        }

        _awaitingWorkspaceEditInstruction = false;

        var workspace = _preferences.Workspace;
        if (IsVoiceCancellation(SpanishVoiceTranscriptNormalizer.Normalize(prompt)))
        {
            _assistantView.AddKohanaMessage("De acuerdo, no cambio nada.");
            return true;
        }

        // Se vuelve a comprobar aquí: el permiso pudo revocarse o bajarse de nivel entre el comando
        // y la respuesta, y eso tiene que surtir efecto en el acto.
        if (!workspace.HasAuthorizedFolder ||
            !WorkspaceAutonomyPolicy.CanWrite(workspace.AutonomyLevel))
        {
            _assistantView.AddKohanaMessage(
                "Ya no tengo permiso para modificar ese proyecto, así que no cambié nada.");
            return true;
        }

        var files = _workspaceReader.ListFiles(workspace.AuthorizedPath, maximumFiles: 500);
        var structure = WorkspaceContextBuilder.BuildStructure(
            workspace.AuthorizedPath, files, workspace.AutonomyLevel);

        _pendingWorkspaceContext = string.IsNullOrWhiteSpace(structure)
            ? WorkspaceEditParser.ModelInstructions
            : structure + Environment.NewLine + WorkspaceEditParser.ModelInstructions;

        _pendingWorkspaceEditRequested = true;
        await SendPromptToAiAsync(prompt, fromVoice);
        return true;
    }

    /// <summary>
    /// Diseño D14 — vista previa y confirmación. La confirmación dice el archivo, si existe o se
    /// crearía, y cuánto ocupa lo nuevo: aceptar un cambio sin saber sobre qué archivo cae es
    /// aceptar a ciegas.
    /// </summary>
    private void OfferWorkspaceEdit(string answer)
    {
        var edit = WorkspaceEditParser.Parse(answer);
        if (edit is null)
        {
            return;
        }

        var workspace = _preferences.Workspace;

        // Diseño D16 — el broker decide primero si la capacidad puede actuar siquiera. Solo se mira
        // la denegación: la confirmación de este camino es el diálogo de abajo, que dice qué archivo
        // y por qué, y preguntar dos veces enseñaría a aceptar sin leer.
        var permission = PermissionBroker.Decide(
            new PermissionRequest(KohanaCapability.Proyecto, edit.Description),
            _preferences.Permissions);

        if (permission.IsDenied)
        {
            _assistantView.AddKohanaMessage(permission.Reason);
            return;
        }

        var verdict = _workspaceEditCoordinator.CanApply(edit, workspace);
        if (!verdict.IsAllowed)
        {
            _assistantView.AddKohanaMessage($"No puedo aplicar ese cambio: {verdict.Message}");
            return;
        }

        var fullPath = Path.Combine(workspace.AuthorizedPath, edit.RelativePath);
        var exists = File.Exists(fullPath);

        var confirmation = MessageBox.Show(
            this,
            $"{(exists ? "Voy a REEMPLAZAR" : "Voy a CREAR")} este archivo:{Environment.NewLine}" +
                $"{edit.RelativePath}{Environment.NewLine}{Environment.NewLine}" +
                $"Motivo: {edit.Description}{Environment.NewLine}" +
                $"Tamaño del contenido nuevo: {edit.NewContent.Length} caracteres." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                "Guardaré una copia previa para que puedas deshacerlo. ¿Lo aplico?",
            "Aplicar un cambio en el proyecto",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            _assistantView.AddKohanaMessage("No apliqué el cambio.");
            return;
        }

        var result = _workspaceEditCoordinator.Apply(edit, workspace);

        _assistantView.AddKohanaMessage(result.Detail);
        ShowFlowNotice(
            result.Success ? CapsuleKind.Success : CapsuleKind.Warning,
            result.Success ? "Cambio aplicado" : "No apliqué el cambio",
            result.Detail);

        RefreshAuditPanel();
    }

    private CommandExecutionResult UndoLastWorkspaceEdit()
    {
        var result = _workspaceEditCoordinator.RevertLast(_preferences.Workspace);

        _assistantView.AddKohanaMessage(result.Detail);
        RefreshAuditPanel();

        if (!result.Success)
        {
            return CommandExecutionResult.Failure(result.Detail);
        }

        ShowFlowNotice(CapsuleKind.Success, "Cambio deshecho", result.Detail);
        return CommandExecutionResult.Success(result.Detail);
    }

    private void ShowWorkspaceStatus()
    {
        var workspace = _preferences.Workspace;

        var message = new StringBuilder();
        if (!workspace.HasAuthorizedFolder)
        {
            message.Append(
                "No tengo ninguna carpeta de proyecto autorizada. Puedes autorizar una desde la " +
                "paleta de comandos, y quitármela igual de rápido.");
        }
        else
        {
            message.AppendLine($"Carpeta autorizada: {workspace.AuthorizedPath}");
            message.AppendLine(
                $"Autorizada el {workspace.AuthorizedAt?.ToString("dd/MM/yyyy HH:mm") ?? "—"}.");
            message.AppendLine(WorkspaceAutonomyPolicy.Describe(workspace.AutonomyLevel));
            message.AppendLine(
                "Solo lectura. No leo .env, claves privadas ni carpetas de dependencias, y oculto " +
                "los valores que parecen secretos antes de enviar nada.");

            var files = _workspaceReader.ListFiles(workspace.AuthorizedPath, maximumFiles: 500);
            message.Append($"Archivos legibles ahora mismo: {files.Count}.");
        }

        _assistantView.AddKohanaMessage(message.ToString().TrimEnd());
        NavigateTo("Assistant", animate: true);
    }

    private CommandExecutionResult RevokeWorkspace()
    {
        if (!_preferences.Workspace.HasAuthorizedFolder)
        {
            return CommandExecutionResult.Failure("No hay ninguna carpeta autorizada.");
        }

        var revoked = _preferences.Workspace.AuthorizedPath;
        _preferences.Workspace.Revoke();
        _preferences.Workspace.Normalize();
        SavePreferences();

        RecordAudit(
            AuditCapability.Permisos,
            "Acceso al proyecto revocado",
            revoked,
            "Revocación explícita del usuario");

        ShowFlowNotice(
            CapsuleKind.Success,
            "Acceso revocado",
            "Ya no puedo leer esa carpeta.");
        return CommandExecutionResult.Success("Ya no puedo leer esa carpeta.");
    }

    /// <summary>
    /// Diseño D12 — explica el proyecto usando la estructura, no el código entero. Mandar un
    /// proyecto completo a un proveedor remoto para que diga de qué va es desproporcionado; con el
    /// árbol de archivos se explica casi igual de bien y sale del equipo una fracción de lo mismo.
    /// </summary>
    private async Task<CommandExecutionResult> ExplainWorkspaceAsync()
    {
        var workspace = _preferences.Workspace;
        if (!workspace.HasAuthorizedFolder)
        {
            return CommandExecutionResult.Failure("Todavía no autorizaste ninguna carpeta de proyecto.");
        }

        var files = _workspaceReader.ListFiles(workspace.AuthorizedPath, maximumFiles: 500);
        if (files.Count == 0)
        {
            return CommandExecutionResult.Failure(
                "No encontré archivos legibles en esa carpeta. Puede que sea todo binarios o dependencias.");
        }

        _pendingWorkspaceContext = WorkspaceContextBuilder.BuildStructure(
            workspace.AuthorizedPath, files, workspace.AutonomyLevel);

        await ProcessPromptAsync(
            "Explícame de qué trata este proyecto y por dónde empezaría a leerlo.",
            fromVoice: false);

        return CommandExecutionResult.Success();
    }

    private IEnumerable<KohanaCommandDescriptor> BuildFocusStartCommands()
    {
        (int Minutes, string Id)[] presets =
        [
            (15, "focus.start.15"),
            (25, "focus.start.25"),
            (45, "focus.start.45")
        ];

        foreach (var (minutes, id) in presets)
        {
            yield return new KohanaCommandDescriptor(
                id,
                $"Iniciar enfoque · {minutes} min",
                $"Comienza una sesión de enfoque de {minutes} minutos.",
                KohanaCommandCategory.Focus,
                _ =>
                {
                    _focusView.StartPreset(TimeSpan.FromMinutes(minutes));
                    CheckFocusTimer();
                    return Task.FromResult(CommandExecutionResult.Success());
                },
                keywords: ["enfoque", "iniciar", "concentración", "pomodoro", minutes.ToString(CultureInfo.InvariantCulture)],
                availability: () => _focusManager.GetSnapshot(DateTimeOffset.Now).ActiveTimer is null
                    ? KohanaCommandAvailability.Available
                    : KohanaCommandAvailability.Unavailable("Ya hay una sesión de enfoque en curso."));
        }
    }

    /// <summary>
    /// Diseño D3 — un comando por cada rutina, no un único comando genérico "ejecutar rutina": así
    /// cada una aparece por su propio nombre en la búsqueda y su disponibilidad refleja si esa
    /// rutina en concreto está habilitada. El registro se reconstruye cada vez que se abre el
    /// Command Center (ver ShowCommandCenter), así que esta lista nunca queda desactualizada.
    /// </summary>
    private IEnumerable<KohanaCommandDescriptor> BuildRoutineExecutionCommands()
    {
        foreach (var routine in _routineManager.GetAll())
        {
            var routineId = routine.Id;
            var routineName = routine.Name;
            yield return new KohanaCommandDescriptor(
                $"routine.execute.{routineId}",
                $"Ejecutar {routineName}",
                $"Ejecuta la rutina «{routineName}».",
                KohanaCommandCategory.System,
                async _ =>
                {
                    var current = _routineManager.GetAll().FirstOrDefault(candidate => candidate.Id == routineId);
                    if (current is null)
                    {
                        return CommandExecutionResult.Failure($"La rutina «{routineName}» ya no existe.");
                    }

                    await RunRoutineAsync(current);
                    return CommandExecutionResult.Success();
                },
                keywords: ["rutina", "routine", "ejecutar", routineName],
                availability: () =>
                {
                    var current = _routineManager.GetAll().FirstOrDefault(candidate => candidate.Id == routineId);
                    if (current is null)
                    {
                        return KohanaCommandAvailability.Unavailable("Esta rutina ya no existe.");
                    }

                    return current.IsEnabled
                        ? KohanaCommandAvailability.Available
                        : KohanaCommandAvailability.Unavailable($"La rutina «{routineName}» está desactivada.");
                });
        }
    }

    /// <summary>
    /// Diseño D3 — misma lógica que <c>FocusView.FinishButton_Click</c>, para el comando
    /// "focus.finish" del Command Center y para el mini temporizador global. Diseño D3.1:
    /// FocusOperationResult.Completion ya trae la tarea asociada, así que no hace falta leer el
    /// timer por separado antes de llamar a Finish().
    /// </summary>
    private CommandExecutionResult FinishActiveFocusSession()
    {
        var now = DateTimeOffset.Now;
        var result = _focusManager.Finish(now);
        _focusView.Refresh(now);
        RefreshHomeView();

        if (!result.Success)
        {
            return CommandExecutionResult.Failure(result.Message);
        }

        if (result.Completion is { } completion)
        {
            _focusView.ShowSessionCompletionNotice(completion);
        }

        return CommandExecutionResult.Success(result.Message);
    }

    private CommandExecutionResult ApplyMasterMute(bool muted)
    {
        var result = _audioMixerService.SetMasterMuted(muted);
        if (!result.Succeeded)
        {
            return CommandExecutionResult.Failure(result.Detail);
        }

        // Refresca la vista de Audio para que el estado mostrado no quede desfasado respecto al
        // cambio que se acaba de aplicar. La Task se observa dentro de RefreshAsync.
        _ = _audioView.RefreshAsync(force: true);
        return CommandExecutionResult.Success(result.Title);
    }

    private IEnumerable<KohanaCommandDescriptor> BuildNavigationCommands()
    {
        // Un comando por destino conocido: la lista sale de ShellNavigationPolicy, así que no
        // puede desincronizarse de la navegación real del shell.
        (string Destination, string Title, string[] Keywords)[] destinations =
        [
            (ShellNavigationPolicy.Home, "Ir a Inicio", ["inicio", "home", "principal"]),
            (ShellNavigationPolicy.Assistant, "Ir a Asistente", ["asistente", "chat", "conversación"]),
            (ShellNavigationPolicy.Tasks, "Ir a Hoy", ["hoy", "tareas", "pendientes"]),
            (ShellNavigationPolicy.Focus, "Ir a Enfoque", ["enfoque", "concentración"]),
            (ShellNavigationPolicy.Routines, "Ir a Rutinas", ["rutinas", "automatización"]),
            (ShellNavigationPolicy.Audio, "Ir a Audio", ["audio", "volumen", "sonido"]),
            (ShellNavigationPolicy.Capture, "Ir a Captura", ["captura", "pantalla", "screenshot"]),
            (ShellNavigationPolicy.System, "Ir a Sistema", ["sistema", "estado", "diagnóstico", "hardware"]),
            (ShellNavigationPolicy.Settings, "Ir a Personalizar", ["personalizar", "configuración", "ajustes", "preferencias"])
        ];

        foreach (var (destination, title, keywords) in destinations)
        {
            var target = destination;
            yield return new KohanaCommandDescriptor(
                $"navigate.{target.ToLowerInvariant()}",
                title,
                "Abre esta sección de Kohana.",
                KohanaCommandCategory.Navigation,
                _ =>
                {
                    NavigateTo(target, animate: _preferences.AnimationsEnabled);
                    return Task.FromResult(CommandExecutionResult.Success());
                },
                keywords: keywords);
        }
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

    private void HomeView_RoutinesRequested(object? sender, EventArgs e)
    {
        NavigateTo(ShellNavigationPolicy.Routines, animate: true);
    }

    private void HomeView_NewTaskRequested(object? sender, EventArgs e)
    {
        NavigateTo(ShellNavigationPolicy.Tasks, animate: true);
        _tasksView.OpenNewEditor();
    }

    private void HomeView_StartFocusRequested(object? sender, EventArgs e)
    {
        NavigateTo(ShellNavigationPolicy.Focus, animate: true);
        _focusView.FocusPrimaryControl();
    }

    /// <summary>
    /// Diseño D3 — "Enfocarme" desde una tarea en Hoy. Prepara la asociación en FocusView (la
    /// próxima sesión que se inicie ahí quedará asociada) y navega; no inicia la sesión por sí
    /// solo, para que la persona elija la duración como con cualquier otra sesión.
    /// </summary>
    private void TasksView_FocusRequested(object? sender, TaskFocusRequestedEventArgs e)
    {
        _focusView.PrepareTaskAssociation(e.TaskId, e.TaskTitle);
        NavigateTo(ShellNavigationPolicy.Focus, animate: true);
        _focusView.FocusPrimaryControl();
    }

    /// <summary>
    /// Diseño D3 — el usuario confirmó explícitamente que quiere marcar como completada la tarea
    /// asociada a una sesión de enfoque que acaba de terminar. Nunca ocurre automáticamente.
    /// </summary>
    private void FocusView_CompleteAssociatedTaskRequested(object? sender, TaskFocusRequestedEventArgs e)
    {
        _taskManager.Complete(e.TaskId);
        _tasksView.Refresh();
        RefreshHomeView();
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

        // Diseño D6.3 — el dictado global se registra igual que los demás atajos: como atajo de
        // sistema, para que funcione con Kohana sin foco (que es todo el sentido de dictar en otra
        // aplicación).
        if (_preferences.FlowEnabled &&
            !RegisterHotKey(windowHandle, FlowHotkeyId, ModControl | ModShift, VirtualKeyD))
        {
            _assistantView.AddKohanaMessage(
                "Ctrl + Shift + D ya está siendo utilizado por otra aplicación; el dictado global no quedó disponible.");
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
        _sakuraPillWindow.Hide();

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
        _sakuraPillWindow.Close();
        _ambientHistoryWindow?.Close();
        _lensHighlightOverlay.Close();
        _ambientForegroundTracker.Dispose();
        _commandPaletteWindow.Close();
        // MainWindow desuscribe los eventos de wake word (a través del coordinador) y cancela
        // el token de vida, pero NO libera los tres motores de voz: su propiedad y Dispose
        // viven en KohanaCompositionRoot y se ejecutan en App.OnExit, justo después de que
        // esta ventana se cierre. La guardia _isClosed y el token cancelado impiden que
        // cualquier operación en vuelo o evento encolado opere durante el cierre; el Dispose
        // de cada motor detiene su grabador.
        _voiceCoordinator.WakeWordDetected -= WakeWordService_WakeWordDetected;
        _voiceCoordinator.RecognitionObserved -= WakeWordService_RecognitionObserved;
        if (_aiChatService is IDisposable disposableAiService)
        {
            disposableAiService.Dispose();
        }
        _trayIcon.Dispose();
        _lifetimeCancellation.Dispose();

        var windowHandle = new WindowInteropHelper(this).Handle;
        if (windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(windowHandle, ShellHotkeyId);
            UnregisterHotKey(windowHandle, PeekHotkeyId);
            UnregisterHotKey(windowHandle, CommandPaletteHotkeyId);
            UnregisterHotKey(windowHandle, LookHotkeyId);
            UnregisterHotKey(windowHandle, FlowHotkeyId);
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
        else if (wParam.ToInt32() == FlowHotkeyId)
        {
            ToggleFlowDictation();
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

        // Diseño D3.1: mientras Kohana estaba oculto en la bandeja, _focusTickTimer siguió
        // corriendo (nunca se detiene salvo al cerrar), así que el dominio está al día — pero el
        // reloj visible de Enfoque (y el resumen de Inicio) no se refrescan solos hasta el
        // siguiente tick de hasta un segundo. Se sincroniza de inmediato al reactivar, igual que
        // ya hace HandleSystemResume() al volver de suspensión.
        CheckFocusTimer();
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
        _latestOllamaRuntimeSnapshot = snapshot;

        if (_isClosed)
        {
            return;
        }

        RefreshAdaptiveEnginePlan();

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
        // Diseño D2: Ctrl + K abre el Sakura Command Center. Se enlaza a la ventana, no con
        // RegisterHotKey, porque sus acciones operan sobre el shell ya visible; capturar Ctrl + K
        // en todo el sistema se lo quitaría a cualquier otra aplicación. Alt + A, Alt + Shift + A,
        // Ctrl + Espacio y Ctrl + Shift + Espacio siguen siendo globales y no se tocan.
        if (e.Key == Key.K && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            ShowCommandCenter();
            e.Handled = true;
            return;
        }

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

            // Diseño D10 — antes de los parsers: "recuerda que prefiero X" es una orden de memoria,
            // no una consulta a la IA. Va después de AddUserMessage para que la frase quede en la
            // conversación igual que cualquier otra.
            if (TryHandleWorkspaceSearchPrompt(prompt))
            {
                return;
            }

            if (TryHandleComputerUseIntent(prompt))
            {
                return;
            }

            if (await TryHandleWorkspaceEditInstructionAsync(prompt, fromVoice))
            {
                return;
            }

            if (TryHandleMemoryPrompt(prompt))
            {
                return;
            }

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

        // Diseño D14 — respuesta que trae un cambio propuesto, si la hubo. Se ofrece fuera del
        // bloque, con el turno de IA ya liberado.
        string? proposedEditAnswer = null;

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

            // Diseño D10 — la memoria se usa, no solo se guarda: sin esto no hay continuidad
            // ninguna entre sesiones. El constructor decide qué sale (solo categorías activas
            // ahora, acotado y redactado de nuevo), porque este texto viaja al proveedor.
            var memoryContext = MemoryContextBuilder.Build(
                _memoryManager.GetAll(_preferences.Memory, DateTimeOffset.Now),
                _preferences.Memory,
                DateTimeOffset.Now);

            if (!string.IsNullOrWhiteSpace(memoryContext))
            {
                systemContext = string.IsNullOrWhiteSpace(systemContext)
                    ? memoryContext
                    : systemContext + Environment.NewLine + memoryContext;
            }

            // Diseño D12 — el contexto del proyecto se consume aquí y se apaga. Que dure una sola
            // consulta es la garantía de que autorizar una carpeta no convierte cada pregunta
            // posterior en un envío de código.
            var workspaceContext = _pendingWorkspaceContext;
            _pendingWorkspaceContext = null;

            if (!string.IsNullOrWhiteSpace(workspaceContext))
            {
                systemContext = string.IsNullOrWhiteSpace(systemContext)
                    ? workspaceContext
                    : systemContext + Environment.NewLine + workspaceContext;
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

            // Diseño D14 — solo si esta consulta se pidió para cambiar algo. Se anota y se ofrece
            // DESPUÉS de soltar el turno de IA: la confirmación es un diálogo modal, y dejar el
            // turno tomado mientras alguien lo lee bloquearía cualquier otra consulta.
            if (_pendingWorkspaceEditRequested)
            {
                proposedEditAnswer = finalText;
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
            // Si la respuesta se cortó, se canceló o falló, la oferta de escribir no sobrevive: un
            // cambio propuesto a partir de media respuesta no es un cambio, es un riesgo.
            _pendingWorkspaceEditRequested = false;
            _assistantView.SetAiActivity(null);
            _aiGate.Release();
        }

        if (proposedEditAnswer is not null)
        {
            OfferWorkspaceEdit(proposedEditAnswer);
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

        // VoiceCoordinator.InputDeviceNumber aplica el valor a la entrada de voz y al wake
        // word en un único setter: efecto idéntico a las dos asignaciones directas que
        // sustituyó (confirmado leyendo VoiceCoordinator.cs antes de este
        // cambio), en el mismo orden (entrada de voz primero, wake word después).
        _voiceCoordinator.InputDeviceNumber = selectedDeviceNumber;
        _settingsView.SetVoiceInputDevices(devices, selectedDeviceNumber);
        SavePreferences();
    }

    private async Task ChangeVoiceInputDeviceAsync(int deviceNumber)
    {
        await using var voiceScope = await _voiceCoordinator.AcquireVoiceInputScopeAsync();
        try
        {
            await PauseWakeWordAsync();

            // La cancelación de la entrada de voz corre sobre el ámbito de voz del
            // coordinador, el único dominio de exclusión para Whisper.
            await voiceScope.CancelAsync();

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
            await ResumeWakeWordIfEnabledAsync();
        }

        // El ámbito de voz se libera aquí, al salir del método (después de la reanudación
        // del finally), preservando el orden reanudar → liberar del código anterior.
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
        await using var voiceScope = await _voiceCoordinator.AcquireVoiceInputScopeAsync();
        var listeningStarted = false;

        try
        {
            await PauseWakeWordAsync();
            _voiceCoordinator.StopSpeaking();

            if (!_voiceCoordinator.IsVoiceInputReady)
            {
                await PrepareVoiceAsync();
                if (!_voiceCoordinator.IsVoiceInputReady)
                {
                    return;
                }
            }

            var result = await voiceScope.StartListeningAsync();

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
        }
    }

    private async void AssistantView_VoiceInputStopped(object? sender, EventArgs e)
    {
        await using var voiceScope = await _voiceCoordinator.AcquireVoiceInputScopeAsync();
        try
        {
            if (!_voiceCoordinator.IsVoiceInputListening)
            {
                return;
            }

            _assistantView.SetVoiceState(
                AssistantVoiceState.Processing,
                "Transcribiendo localmente con Whisper…");

            var result = await voiceScope.StopListeningAsync();
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

        if (!_voiceCoordinator.IsWakeWordListening)
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
        _voiceCoordinator.WakeWordCustomAliases = _preferences.WakeWordAliases;
        SavePreferences();
        _settingsView.SetWakeWordAliases(_preferences.WakeWordAliases);
        _settingsView.SetWakeWordTestStatus($"Alias “{alias}” guardado.", isSuccess: true);
        await ApplyWakeWordPreferenceAsync(showCapsule: false);
    }

    private async Task ClearWakeWordAliasesAsync()
    {
        _preferences.WakeWordAliases.Clear();
        _voiceCoordinator.WakeWordCustomAliases = [];
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
            _voiceCoordinator.IsWakeWordListening ? CapsuleKind.Success : CapsuleKind.Warning,
            _voiceCoordinator.IsWakeWordListening ? "Voz reiniciada" : "Voz no disponible",
            _voiceCoordinator.IsWakeWordListening
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
        await using var voiceScope = await _voiceCoordinator.AcquireVoiceInputScopeAsync();
        try
        {
            await PauseWakeWordAsync();
            _voiceCoordinator.StopSpeaking();

            if (!_voiceCoordinator.IsVoiceInputReady)
            {
                await PrepareVoiceAsync();
                if (!_voiceCoordinator.IsVoiceInputReady)
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

            var result = await voiceScope.ListenForUtteranceAsync(
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
        await using var wakeWordScope = await _voiceCoordinator.AcquireWakeWordScopeAsync();
        try
        {
            await wakeWordScope.StopListeningAsync();
            SetWakeWordIndicator(active: false);
            RefreshRuntimeDashboard();

            if (!_preferences.WakeWordEnabled || _isClosed || _voiceCoordinator.IsVoiceInputListening)
            {
                return;
            }

            if (_preferences.PauseWakeWordInGameMode && _resourceDecision.PauseWakeWord)
            {
                SetWakeWordIndicator(active: false);
                _assistantView.SetVoiceAvailability(
                    _voiceCoordinator.IsVoiceInputReady,
                    "Activación por voz pausada por Modo Juego.");
                return;
            }

            var requiresDownload = !_voiceCoordinator.IsWakeWordReady;
            var progress = new Progress<VoicePreparationProgress>(update =>
            {
                WakeWordIndicatorText.Text = "Preparando voz";
                WakeWordIndicator.Visibility = Visibility.Visible;
                _assistantView.SetVoiceAvailability(
                    _voiceCoordinator.IsVoiceInputReady,
                    update.Detail);
            });

            var preparation = await _voiceCoordinator.PrepareWakeWordAsync(
                progress,
                _lifetimeCancellation.Token);

            if (!preparation.IsReady)
            {
                SetWakeWordIndicator(active: false);
                _assistantView.SetVoiceAvailability(
                    _voiceCoordinator.IsVoiceInputReady,
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

            _voiceCoordinator.WakeWordSensitivity = _preferences.WakeWordSensitivity;
            _voiceCoordinator.WakeWordCustomAliases = _preferences.WakeWordAliases;
            var start = await wakeWordScope.StartListeningAsync(
                _preferences.WakeWordPhrase,
                _lifetimeCancellation.Token);

            if (!start.IsAvailable)
            {
                SetWakeWordIndicator(active: false);
                _assistantView.SetVoiceAvailability(
                    _voiceCoordinator.IsVoiceInputReady,
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
                _voiceCoordinator.IsVoiceInputReady,
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

        // El ámbito de wake word se libera aquí, al salir del método (igual que el
        // antiguo finally): la sección crítica conserva exactamente la misma duración.
    }

    private async Task PauseWakeWordAsync()
    {
        await using var wakeWordScope = await _voiceCoordinator.AcquireWakeWordScopeAsync();
        await wakeWordScope.StopListeningAsync();
        SetWakeWordIndicator(active: false);
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
            // Diseño D3: el runner no conoce el registro de rutinas (no le corresponde); se
            // registra aquí, justo después, con el resultado real de la ejecución.
            _routineManager.RecordExecution(routine.Id, report.CompletedAt, report.Succeeded);
            _assistantView.AddKohanaMessage(report.BuildSummary());
            _tasksView.Refresh();
            _focusView.Refresh(DateTimeOffset.Now);
            await _audioView.RefreshAsync(force: true);
            _routinesView.Refresh();
            RefreshHomeView();
            _dailyFlowHub.RaiseRoutinesChanged();

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
        _focusContinuity.Refresh(now);

        // El mini temporizador no se muestra dentro de la propia sección Enfoque: mostraría el
        // mismo estado dos veces en la misma pantalla.
        if (_currentDestination.Equals(ShellNavigationPolicy.Focus, StringComparison.OrdinalIgnoreCase))
        {
            FocusMiniTimerControl.Visibility = Visibility.Collapsed;
        }

        _dailyFlowHub.RaiseFocusChanged();

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

        // Diseño D3.1: el aviso no modal de fin de sesión se muestra para toda finalización
        // natural, con tarea asociada o sin ella — no solo cuando hay una tarea que ofrecer
        // completar. El capsule/tray de arriba es un aviso ambiental transitorio (se ve desde
        // cualquier sección); este otro vive en Enfoque con las acciones reales (completar tarea,
        // iniciar otra sesión, cerrar).
        _focusView.ShowSessionCompletionNotice(completion);
    }

    /// <summary>
    /// Diseño D4 — punto único de refresco del Sakura Pill Host, igual que
    /// <see cref="CheckFocusTimer"/> lo es para el mini temporizador. Se llama después de cualquier
    /// mutación de <see cref="_ambientRequestManager"/> (comandos del Command Center, botones de la
    /// propia ventana a través de <see cref="_ambientCoordinator"/>).
    /// </summary>
    private void CheckAmbientRequest() => _ambientCoordinator.Refresh();

    private void AmbientCoordinator_QuickActionInvoked(object? sender, string actionId)
    {
        if (!string.Equals(actionId, "copy", StringComparison.Ordinal))
        {
            return;
        }

        var text = _ambientRequestManager.GetSnapshot(recentCount: 0).ActiveRequest?.Result?.ShortText;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        try
        {
            Clipboard.SetText(text);
        }
        catch (Exception exception) when (
            exception is COMException or ExternalException)
        {
            // El portapapeles puede estar bloqueado por otro proceso; no es un fallo de Kohana.
        }
    }

    /// <summary>
    /// Diseño D4 — comando inicial del Command Center para el Sakura Pill Host: captura un
    /// Context Snapshot real de la última ventana externa en primer plano
    /// (<see cref="_ambientForegroundTracker"/>, actualizado en tiempo real) y recorre el ciclo
    /// completo Escuchando → Pensando → Resultado/Error. No hay procesamiento de IA todavía (eso
    /// llega con Lens/Flow, Fases 2-3): el "resultado" es honesto sobre lo que puede observar hoy —
    /// título y proceso de la ventana activa.
    /// </summary>
    private async Task<CommandExecutionResult> ExecuteAmbientContextPeekAsync()
    {
        var now = DateTimeOffset.Now;
        var context = _ambientContextProvider.Capture(_ambientForegroundTracker.LastExternalWindowHandle);
        var beginResult = _ambientRequestManager.Begin("¿Qué ventana tengo activa?", context, now);
        if (!beginResult.Success)
        {
            return CommandExecutionResult.Failure(beginResult.Message);
        }

        CheckAmbientRequest();
        _ambientRequestManager.BeginThinking(DateTimeOffset.Now);
        CheckAmbientRequest();

        await Task.Delay(TimeSpan.FromMilliseconds(450));

        if (context is null || string.IsNullOrWhiteSpace(context.WindowTitle))
        {
            var message = context is { IsSensitive: true }
                ? "Esa ventana está marcada como sensible; Kohana no expone su título."
                : "No pude identificar una ventana activa distinta de Kohana.";
            _ambientRequestManager.Fail(message, DateTimeOffset.Now);
        }
        else
        {
            var shortText = string.IsNullOrWhiteSpace(context.ProcessName)
                ? context.WindowTitle
                : $"{context.WindowTitle} — {context.ProcessName}";
            var result = new AmbientRequestResult(
                shortText,
                $"Proceso: {context.ProcessName}\nTítulo completo: {context.WindowTitle}",
                [new AmbientQuickAction("copy", "Copiar", AmbientAutonomyLevel.Ver)],
                CanUndo: false);
            _ambientRequestManager.CompleteWithResult(result, DateTimeOffset.Now);
        }

        CheckAmbientRequest();
        return CommandExecutionResult.Success();
    }

    /// <summary>
    /// Diseño D5.5/D5.6 (Fase 2 — Kohana Lens) — captura la ventana activa, la procesa con OCR y
    /// UI Automation (ambos redactados de contenido sensible antes de usarse en cualquier lugar,
    /// incluida la propia imagen — ver <see cref="SensitiveContentRedactor"/>/<see cref="ImageRedactor"/>),
    /// arma el contexto del modo elegido y pregunta a la IA. El resultado se muestra en el mismo
    /// Sakura Pill Host de D4 — Lens es, en esencia, otra fuente de solicitudes ambientales, no una
    /// superficie nueva. El indicador "Mirando" (<see cref="LensIndicator"/>) permanece visible
    /// mientras dura la captura y el análisis, nunca más — el modelo de confianza exige que ese
    /// paso sea siempre visible, nunca silencioso.
    /// </summary>
    private async Task<CommandExecutionResult> ExecuteLensAsync(LensMode mode)
    {
        var now = DateTimeOffset.Now;
        var context = _ambientContextProvider.Capture(_ambientForegroundTracker.LastExternalWindowHandle);

        var beginResult = _ambientRequestManager.Begin(
            $"Kohana Lens — {LensModeLabel(mode)}", context, now);
        if (!beginResult.Success)
        {
            return CommandExecutionResult.Failure(beginResult.Message);
        }

        CheckAmbientRequest();
        _ambientRequestManager.BeginThinking(DateTimeOffset.Now);
        CheckAmbientRequest();

        LensIndicator.Visibility = Visibility.Visible;
        try
        {
            var windowHandle = _ambientForegroundTracker.LastExternalWindowHandle;
            if (context is null || context.IsSensitive || windowHandle == 0)
            {
                var message = context is { IsSensitive: true }
                    ? "Esa ventana está marcada como sensible; Kohana Lens no la observa."
                    : "No pude identificar una ventana activa distinta de Kohana.";
                _ambientRequestManager.Fail(message, DateTimeOffset.Now);
                return CommandExecutionResult.Success();
            }

            var target = new VisionCaptureTarget(
                $"window:{windowHandle}",
                windowHandle,
                context.WindowTitle ?? "Ventana activa",
                context.ProcessName ?? string.Empty,
                VisionCaptureKind.Window,
                0, 0, 0, 0);

            var captureResult = await _screenCaptureService.CaptureAsync(target);
            if (!captureResult.IsSuccess || captureResult.PngBytes is null)
            {
                _ambientRequestManager.Fail(
                    $"No pude capturar la ventana: {captureResult.Detail}", DateTimeOffset.Now);
                return CommandExecutionResult.Success();
            }

            var ocrResult = await _lensOcrService.RecognizeAsync(captureResult.PngBytes);
            var uiaSnapshot = _lensUiAutomationReader.Read(windowHandle);

            var redactedOcr = SensitiveContentRedactor.Redact(ocrResult);
            var redactedElements = SensitiveContentRedactor.Redact(uiaSnapshot.Elements);
            var sensitiveLines = SensitiveContentRedactor.FindSensitiveLines(ocrResult);
            var imageBytes = ImageRedactor.RedactRegions(captureResult.PngBytes, sensitiveLines);

            var lensContext = LensContextBuilder.Build(
                mode,
                context.WindowTitle ?? captureResult.Title,
                redactedOcr,
                redactedElements);

            var image = AiImageAttachment.FromBytes(imageBytes);
            var messages = new[]
            {
                new ConversationMessage(ConversationRole.User, lensContext.Prompt, DateTimeOffset.Now)
            };

            var aiRequest = new AiChatRequest(
                messages,
                NexoAiInstructions.Default,
                lensContext.SystemContext,
                [image],
                lensContext.RequestMode);

            // Diseño D7 — se transmite por partes en vez de esperar la respuesta completa: el
            // usuario ve el texto aparecer conforme la IA lo escribe, que es lo que pidió tras
            // probar Lens. Si el proveedor falla a mitad, lo ya recibido no se descarta en
            // silencio: se conserva como resultado parcial y se avisa.
            _ambientRequestManager.BeginStreaming(DateTimeOffset.Now);
            CheckAmbientRequest();

            var streamed = new StringBuilder();
            string? streamFailure = null;
            try
            {
                await foreach (var chunk in _aiChatService.StreamAsync(
                    BuildAiConfiguration(), aiRequest, _lifetimeCancellation.Token))
                {
                    if (string.IsNullOrEmpty(chunk))
                    {
                        continue;
                    }

                    streamed.Append(chunk);
                    _ambientRequestManager.AppendStreamedText(chunk, DateTimeOffset.Now);
                    CheckAmbientRequest();
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                streamFailure = exception.Message;
            }

            if (streamed.Length == 0)
            {
                _ambientRequestManager.Fail(
                    string.IsNullOrWhiteSpace(streamFailure)
                        ? "No pude analizar la ventana activa."
                        : streamFailure,
                    DateTimeOffset.Now);
            }
            else
            {
                var answer = streamed.ToString();
                _ambientRequestManager.CompleteStreamedResult(
                    [], canUndo: false, DateTimeOffset.Now, SummarizeForCapsule);
                ShowLensHighlights(answer, redactedOcr, redactedElements, uiaSnapshot);

                if (streamFailure is not null)
                {
                    ShowFlowNotice(
                        CapsuleKind.Warning,
                        "Respuesta incompleta",
                        "La conexión se cortó mientras respondía; lo que alcanzó a escribir sigue visible.");
                }
            }
        }
        finally
        {
            LensIndicator.Visibility = Visibility.Collapsed;
        }

        CheckAmbientRequest();
        return CommandExecutionResult.Success();
    }

    /// <summary>
    /// Diseño D5.7 — resalta, sobre la ventana real observada, las regiones de OCR/UI Automation
    /// que la respuesta de la IA parece mencionar (ver <see cref="LensHighlightMatcher"/> para la
    /// heurística y sus límites). Se omite en silencio si la lectura de UI Automation falló o no
    /// tiene límites válidos: sin ellos no hay dónde posicionar la superposición con confianza.
    /// </summary>
    private void ShowLensHighlights(
        string answerText,
        OcrResult redactedOcr,
        IReadOnlyList<UiAutomationElement> redactedElements,
        UiAutomationSnapshot uiaSnapshot)
    {
        if (!uiaSnapshot.IsSuccess || uiaSnapshot.WindowWidth <= 0 || uiaSnapshot.WindowHeight <= 0)
        {
            return;
        }

        var regions = LensHighlightMatcher.FindMatches(
            answerText, redactedOcr, redactedElements, uiaSnapshot.WindowLeft, uiaSnapshot.WindowTop);

        _lensHighlightOverlay.ShowHighlights(
            uiaSnapshot.WindowLeft,
            uiaSnapshot.WindowTop,
            uiaSnapshot.WindowWidth,
            uiaSnapshot.WindowHeight,
            regions);
    }

    /// <summary>
    /// Diseño D6.3 (Fase 3 — Kohana Flow) — el atajo global funciona como interruptor: una pulsación
    /// empieza a dictar, la siguiente termina y escribe.
    ///
    /// No es "mantener presionado" por una razón concreta: <c>RegisterHotKey</c> solo avisa de la
    /// pulsación, nunca del soltado, así que un verdadero push-to-talk exigiría un hook de teclado
    /// de bajo nivel — la misma API que usan los registradores de teclas, que dispara falsos
    /// positivos de antivirus y encaja mal con el empaquetado para la Store. Además, el valor que
    /// el roadmap le atribuye a Flow es "escribir texto largo por voz", y sostener una tecla dos
    /// minutos es peor experiencia que alternar. El botón de micrófono que ya existía en el
    /// Asistente también es un interruptor, así que esto es consistente con lo que ya había.
    /// </summary>
    /// <summary>
    /// Diseño D7 — aplica el atajo global sin reiniciar Kohana: al activarlo desde Ajustes se
    /// registra en el momento, y al desactivarlo se libera para que otra aplicación pueda usar la
    /// combinación. Registrar dos veces el mismo id es inofensivo (Windows lo rechaza y se ignora),
    /// así que se desregistra siempre antes.
    /// </summary>
    private void ApplyFlowHotkeyRegistration()
    {
        var windowHandle = new WindowInteropHelper(this).Handle;
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        UnregisterHotKey(windowHandle, FlowHotkeyId);

        if (_preferences.FlowEnabled &&
            !RegisterHotKey(windowHandle, FlowHotkeyId, ModControl | ModShift, VirtualKeyD))
        {
            _assistantView.AddKohanaMessage(
                "Ctrl + Shift + D ya está siendo utilizado por otra aplicación; el dictado global no quedó disponible.");
        }
    }

    private void ToggleFlowDictation()
    {
        if (_isClosed || !_preferences.FlowEnabled)
        {
            return;
        }

        var pendingStop = _flowStopSignal;
        if (pendingStop is not null)
        {
            // Ya hay un dictado en curso: esta pulsación lo cierra.
            pendingStop.TrySetResult(true);
            return;
        }

        _ = RunFlowDictationAsync();
    }

    private async Task RunFlowDictationAsync()
    {
        // La ventana destino se fija AQUÍ, antes de grabar nada: es la referencia contra la que el
        // insertor comprobará después que el foco no cambió.
        _flowTargetWindowHandle = _ambientForegroundTracker.LastExternalWindowHandle;

        var stopSignal = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _flowStopSignal = stopSignal;
        FlowIndicator.Visibility = Visibility.Visible;

        try
        {
            await using var voiceScope = await _voiceCoordinator.AcquireVoiceInputScopeAsync();
            await PauseWakeWordAsync();
            _voiceCoordinator.StopSpeaking();

            try
            {
                var startResult = await voiceScope.StartListeningAsync();
                if (!startResult.IsAvailable)
                {
                    ShowFlowNotice(CapsuleKind.Warning, "No pude escuchar", startResult.Detail);
                    return;
                }

                // El ámbito de voz debe sostenerse durante toda la sección crítica, así que se
                // espera aquí a la segunda pulsación en vez de repartir el ámbito entre dos
                // manejadores de mensajes distintos.
                await stopSignal.Task;

                var recognition = await voiceScope.StopListeningAsync(
                    VoiceTranscriptionMode.Dictation);

                HandleFlowDictationResult(recognition);
            }
            finally
            {
                await ResumeWakeWordIfEnabledAsync();
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        {
            ShowFlowNotice(CapsuleKind.Error, "Dictado interrumpido", exception.Message);
        }
        finally
        {
            _flowStopSignal = null;
            FlowIndicator.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Diseño D6.3 — rama propia para el dictado: NO pasa por
    /// <c>HandleVoiceRecognitionResultAsync</c> ni por <c>ProcessPromptAsync</c>. Lo dictado es
    /// texto que la persona quiere escribir en otra aplicación, no una orden para Kohana; mezclarlo
    /// con el camino de comandos haría, por ejemplo, que dictar "abre Spotify" dentro de un correo
    /// abriera Spotify en vez de escribir esas palabras.
    /// </summary>
    private void HandleFlowDictationResult(VoiceRecognitionResult recognition)
    {
        if (!recognition.IsRecognized)
        {
            ShowFlowNotice(CapsuleKind.Warning, "No te entendí", recognition.Detail);
            return;
        }

        var options = new FlowDictationOptions(
            _preferences.FlowMode,
            FlowSettingsParser.ParseDictionary(_preferences.FlowDictionary),
            FlowSettingsParser.ParseSnippets(_preferences.FlowSnippets));

        var text = SpanishDictationNormalizer.Normalize(recognition.Text, options);
        var insertion = _flowTextInserter.Insert(text, _flowTargetWindowHandle);

        if (insertion.IsInserted)
        {
            ShowFlowNotice(CapsuleKind.Success, "Dictado escrito", SummarizeForCapsule(text));
            return;
        }

        HandleFlowInsertionFailure(insertion, text);
    }

    /// <summary>
    /// Diseño D6.3 — cuando no se pudo escribir, el texto dictado NO se tira. Se copia al
    /// portapapeles para que no se pierda el trabajo… salvo si la ventana destino era sensible: en
    /// ese caso lo dictado pudo ser una contraseña, y dejarla en el portapapeles (accesible a
    /// cualquier otro programa) sería peor que perderla.
    /// </summary>
    private void HandleFlowInsertionFailure(FlowInsertionResult insertion, string text)
    {
        if (insertion.Failure == FlowInsertionFailure.SensitiveWindow)
        {
            ShowFlowNotice(CapsuleKind.Warning, "Dictado descartado", insertion.Detail);
            return;
        }

        if (insertion.Failure == FlowInsertionFailure.EmptyText || string.IsNullOrEmpty(text))
        {
            ShowFlowNotice(CapsuleKind.Warning, "No te entendí", insertion.Detail);
            return;
        }

        try
        {
            Clipboard.SetText(text);
            ShowFlowNotice(
                CapsuleKind.Warning,
                "Copiado, no escrito",
                $"{insertion.Detail} Lo dejé en el portapapeles.");
        }
        catch (Exception exception) when (exception is COMException or ExternalException)
        {
            ShowFlowNotice(CapsuleKind.Error, "Dictado perdido", insertion.Detail);
        }
    }

    private void ShowFlowNotice(CapsuleKind kind, string title, string detail) =>
        _capsuleWindow.ShowMessage(kind, title, detail, _preferences.Position);

    private static string LensModeLabel(LensMode mode) => mode switch
    {
        LensMode.Soporte => "modo soporte",
        LensMode.Estudio => "modo estudio",
        LensMode.Desarrollo => "modo desarrollo",
        _ => mode.ToString()
    };

    /// <summary>
    /// Diseño D4.4 — abre (o refresca, si ya estaba abierto) el historial de solicitudes
    /// ambientales. Los datos ya existían desde D4.1 (<c>AmbientRequestManager.GetHistory()</c>,
    /// usados internamente para el archivado automático); esta es la primera superficie visible.
    /// </summary>
    private void ShowAmbientHistory()
    {
        if (_isClosed)
        {
            return;
        }

        if (_ambientHistoryWindow is null)
        {
            _ambientHistoryWindow = new AmbientHistoryWindow();
            _ambientHistoryWindow.UndoRequested += AmbientHistoryWindow_UndoRequested;
        }

        _ambientHistoryWindow.ShowFor(
            this,
            AmbientRequestHistorySummaryBuilder.Build(_ambientRequestManager.GetHistory()));
    }

    private void AmbientHistoryWindow_UndoRequested(object? sender, Guid requestId)
    {
        _ambientRequestManager.Undo(requestId, DateTimeOffset.Now);
        CheckAmbientRequest();
        _ambientHistoryWindow?.Apply(
            AmbientRequestHistorySummaryBuilder.Build(_ambientRequestManager.GetHistory()));
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
        _dailyFlowHub.RaiseTasksChanged();
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
            _voiceCoordinator.Speak(text);
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

        // Diseño D3.1: el temporizador de Enfoque sigue corriendo cada segundo en segundo plano
        // (_focusTickTimer) sin importar qué sección esté activa, así que el dominio nunca se
        // atrasa. Pero antes, solo Inicio forzaba un refresco inmediato de sus propios controles
        // al navegar — Enfoque no lo hacía, así que su reloj visible podía quedarse hasta un
        // segundo desactualizado hasta el siguiente tick programado. Se llama al mismo punto
        // central que ya usa HandleSystemResume() (reanudar desde suspensión) para no duplicar
        // esta condición por cada uno de los nueve destinos: siempre sincroniza tanto Enfoque
        // como Inicio, sin crear un timer nuevo ni cambiar la fuente de verdad (los timestamps).
        CheckFocusTimer();

        if (!animate || !ShellAnimationsAllowed)
        {
            ModuleHost.Opacity = 1;
            ModuleHost.RenderTransform = Transform.Identity;
            FocusCurrentView();
            return;
        }

        ModuleHost.Opacity = 0;
        var transform = new TranslateTransform(14, 0);
        ModuleHost.RenderTransform = transform;

        var easing = (CubicEase)FindResource("MotionEaseOut");
        var duration = (Duration)FindResource("MotionFast");

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

    /// <summary>
    /// Diseño D1 (Sakura Shell): el estado seleccionado nunca depende solo del color. Cada
    /// entrada combina superficie elevada (fondo), indicador tipo "tallo" a la izquierda,
    /// ícono con trazo más grueso, y texto con más peso — la misma combinación que se
    /// verifica en <c>AdaptiveEngineUiInvariantTests</c>-equivalentes de este sprint.
    ///
    /// Diseño D2.0: la cuarta señal era "ícono relleno en vez de solo trazo", pero rellenar
    /// geometrías de línea las convertía en bloques sólidos (y hacía desaparecer las que solo
    /// tienen segmentos). Ahora es el grosor del trazo, que preserva la silueta.
    /// </summary>
    private void UpdateNavigationState(string destination)
    {
        var items = new (string Key, Button Button, Border Indicator, System.Windows.Shapes.Path Icon, TextBlock Label)[]
        {
            ("Home", HomeNavButton, HomeNavIndicator, HomeNavIcon, HomeNavLabel),
            ("Assistant", AssistantNavButton, AssistantNavIndicator, AssistantNavIcon, AssistantNavLabel),
            ("Tasks", TasksNavButton, TasksNavIndicator, TasksNavIcon, TasksNavLabel),
            ("Focus", FocusNavButton, FocusNavIndicator, FocusNavIcon, FocusNavLabel),
            ("Routines", RoutinesNavButton, RoutinesNavIndicator, RoutinesNavIcon, RoutinesNavLabel),
            ("Audio", AudioNavButton, AudioNavIndicator, AudioNavIcon, AudioNavLabel),
            ("Capture", CaptureNavButton, CaptureNavIndicator, CaptureNavIcon, CaptureNavLabel),
            ("System", SystemNavButton, SystemNavIndicator, SystemNavIcon, SystemNavLabel),
            ("Settings", SettingsNavButton, SettingsNavIndicator, SettingsNavIcon, SettingsNavLabel)
        };

        foreach (var item in items)
        {
            var selected = item.Key.Equals(destination, StringComparison.OrdinalIgnoreCase);
            ApplyNavigationItemState(item.Button, item.Indicator, item.Icon, item.Label, selected);
        }
    }

    private void ApplyNavigationItemState(Button button, Border indicator, System.Windows.Shapes.Path icon, TextBlock label, bool selected)
    {
        button.Background = selected
            ? (Brush)FindResource("BrushAccentSoft")
            : Brushes.Transparent;
        button.Foreground = selected
            ? (Brush)FindResource("BrushAccent")
            : (Brush)FindResource("BrushTextSecondary");

        indicator.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        icon.Style = (Style)FindResource(selected ? "SakuraNavigationIconSelectedStyle" : "SakuraNavigationIconStyle");
        label.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
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
        // Diseño D3: el cálculo del resumen vive en DailyFlowSummaryBuilder (Nexo.App/DailyFlow),
        // no aquí — MainWindow solo reúne las entradas y aplica el resultado a la vista.
        var model = DailyFlowSummaryBuilder.BuildHomeDashboard(
            _taskManager,
            _focusManager,
            _routineManager.GetAll(),
            _lastExternalWindowHandle != 0,
            DateTimeOffset.Now);

        _homeView.Refresh(model);
    }

    private void ApplyPreferences()
    {
        _preferences.Normalize();
        Width = _preferences.Width;
        ApplyShellOpacity();
        ApplyAccent(_preferences.AccentColor);
        _voiceCoordinator.WakeWordSensitivity = _preferences.WakeWordSensitivity;
        ApplyModuleVisibility();
        _assistantView.SetVisionAvailability(_preferences.VisionEnabled);
    }

    private void ApplyShellOpacity()
    {
        // Diseño D1: usa el color de fondo real del tema (BrushBackground) en vez de un
        // literal hexadecimal casi-duplicado, para que el shell siga una única fuente de
        // verdad de color. El comportamiento (opacidad configurable del shell) no cambia.
        var baseColor = ((SolidColorBrush)FindResource("BrushBackground")).Color;
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

    private async Task RefreshHardwareCapabilityAsync()
    {
        _systemView.ShowHardwareCapabilityUpdating();
        try
        {
            var profile = await _hardwareCapabilityService.RefreshAsync(_lifetimeCancellation.Token);
            if (_isClosed)
            {
                return;
            }

            _systemView.UpdateHardwareCapability(profile);
            RefreshAdaptiveEnginePlan();
        }
        catch (OperationCanceledException)
        {
            // La ventana se cerró antes de que terminara la detección.
        }
        catch (Exception)
        {
            // La detección de hardware es informativa: un fallo nunca debe afectar el resto de Kohana.
            if (!_isClosed)
            {
                _systemView.UpdateHardwareCapability(_hardwareCapabilityService.GetCachedProfile());
                RefreshAdaptiveEnginePlan();
            }
        }
    }

    private void RefreshAdaptiveEnginePlan()
    {
        if (_isClosed)
        {
            return;
        }

        var hardwareProfile = _hardwareCapabilityService.GetCachedProfile();
        var descriptors = _adaptiveEngineRegistry.GetDescriptors();
        var runtimeStates = _adaptiveEngineRegistry.CaptureRuntimeStates(_preferences, _latestOllamaRuntimeSnapshot);

        var plan = AdaptiveEnginePolicy.Evaluate(
            hardwareProfile,
            _preferences.HardwarePerformanceMode,
            descriptors,
            runtimeStates,
            DateTimeOffset.Now);

        _systemView.UpdateAdaptiveEnginePlan(plan, descriptors);
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

        await _resourceGovernorDecisionGate.WaitAsync();
        try
        {
            var shouldPauseWakeWord =
                _preferences.PauseWakeWordInGameMode && decision.PauseWakeWord;

            if (shouldPauseWakeWord)
            {
                if (_voiceCoordinator.IsWakeWordListening)
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
            _resourceGovernorDecisionGate.Release();
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
            _voiceCoordinator.IsVoiceInputReady,
            _preferences.WakeWordEnabled,
            _voiceCoordinator.IsWakeWordListening,
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
            _voiceCoordinator.Speak(detail);
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
        if (_isClosed || _exitRequested)
        {
            return;
        }

        // Un único inicio de apagado. La fase asíncrona (detener el runtime de IA
        // administrado) se ejecuta MIENTRAS el Dispatcher aún bombea, antes de
        // Application.Shutdown, para no bloquear el hilo de UI con sync-sobre-async durante
        // App.OnExit. No se inician nuevas operaciones después de esto.
        _exitRequested = true;
        _ = RequestExitAsync();
    }

    private async Task RequestExitAsync()
    {
        try
        {
            if (_managedOllamaSupervisor is not null)
            {
                await _managedOllamaSupervisor.StopAsync();
            }
        }
        catch (Exception)
        {
            // El apagado del runtime administrado es best-effort; el cierre continúa igual.
        }
        finally
        {
            _allowExit = true;
            System.Windows.Application.Current.Shutdown();
        }
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
