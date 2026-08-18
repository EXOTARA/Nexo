using Nexo.Core.Ai;
using Nexo.Core.AdaptiveEngine;
using Nexo.Core.Flow;
using Nexo.Core.Memory;
using Nexo.Core.Permissions;
using Nexo.Core.Voice;
using Nexo.Core.Workspace;
using WakePhrase = Nexo.Core.Voice.WakeWordPhrase;
using WakeSensitivity = Nexo.Core.Voice.WakeWordSensitivity;

namespace Nexo.Core.Settings;

public enum SidebarPosition
{
    Left,
    Right
}

public sealed class ShellPreferences
{
    /// <summary>
    /// Diseño D22 — el último escalón de la escalera de migración, con nombre. Existía solo como
    /// número suelto repetido en el último rung y en una veintena de pruebas, y un número repetido es
    /// un número que se olvida de actualizar en algún sitio.
    /// </summary>
    public const int CurrentSchemaVersion = 26;

    public int SchemaVersion { get; set; }
    public SidebarPosition Position { get; set; } = SidebarPosition.Right;

    public double Width { get; set; } = 700;

    public double Opacity { get; set; } = 0.96;

    public string AccentColor { get; set; } = "#E98AAF";

    /// <summary>
    /// De dónde sale el acento. <see cref="AccentColor"/> se conserva siempre aparte, así que
    /// volver a "elegido a mano" devuelve el color tal como estaba.
    /// </summary>
    public AccentSource AccentSource { get; set; }

    public bool AnimationsEnabled { get; set; } = true;

    public bool SideRailExpanded { get; set; }

    /// <summary>
    /// Diseño D52 — qué módulos ocupan sitio fijo en el rail.
    ///
    /// Salvo Sistema, todos llegan apagados. No es que se hayan quitado: cada uno sigue teniendo su
    /// comando «Ir a…» en la paleta, y esta casilla los devuelve al rail cuando alguien los use lo
    /// bastante como para quererlos a un clic. Lo que se retira es su derecho a ocupar espacio
    /// permanente antes de haberlo demostrado.
    /// </summary>
    public bool ShowHomeModule { get; set; }

    public bool ShowTasksModule { get; set; }

    public bool ShowFocusModule { get; set; }

    public bool ShowRoutinesModule { get; set; }

    public bool ShowAudioModule { get; set; }

    public bool ShowCaptureModule { get; set; }

    public bool ShowSystemModule { get; set; } = true;

    /// <summary>
    /// Diseño D27 — llevar el ratón al borde donde está acoplada Kohana la hace aparecer.
    /// Llega activado porque una función que hay que descubrir en Ajustes para saber que existe no
    /// la usa nadie; y se puede apagar aquí mismo porque un borde de pantalla también es donde vive
    /// la barra de desplazamiento de todas las demás ventanas.
    /// </summary>
    public bool EdgeRevealEnabled { get; set; } = true;

    public bool PeekEnabled { get; set; } = true;

    public bool ShowCpuInPeek { get; set; } = true;

    public bool ShowMemoryInPeek { get; set; } = true;

    public bool ShowGpuInPeek { get; set; } = true;

    public bool ShowDiskInPeek { get; set; }

    public bool ShowTopProcessInPeek { get; set; } = true;

    public bool SaveConversationHistory { get; set; }

    public int RecentConversationMessageLimit { get; set; } = 8;

    public bool SpeakVoiceResponses { get; set; }

    public int VoiceInputDeviceNumber { get; set; } = -1;

    public bool WakeWordEnabled { get; set; }

    public WakePhrase WakeWordPhrase { get; set; } = WakePhrase.OyeKohana;

    public WakeSensitivity WakeWordSensitivity { get; set; } = WakeSensitivity.Balanced;

    public List<string> WakeWordAliases { get; set; } = [];

    public AiProviderKind AiProvider { get; set; } = AiProviderKind.Disabled;

    public string AiBaseUrl { get; set; } = string.Empty;

    public string AiModel { get; set; } = string.Empty;

    /// <summary>
    /// Diseño D58 — el modelo elegido para cada proveedor, no uno solo para todos.
    ///
    /// Es la misma razón por la que las claves se guardan por proveedor: cambiar de Groq a Gemini y
    /// volver no debería obligar a reelegir. Antes, cambiar de proveedor pisaba
    /// <see cref="AiModel"/> con el del preset, así que la vuelta no devolvía tu elección sino la
    /// de fábrica — y el que la tenía afinada lo notaba cada vez.
    ///
    /// La clave es el nombre del proveedor y no su número: los números de un enum se reordenan al
    /// añadir uno en medio, y eso convertiría el modelo de Groq en el de otro sin que nadie tocara
    /// nada.
    /// </summary>
    public Dictionary<string, string> AiModelsByProvider { get; set; } = [];

    public string AiApiKeyEnvironmentVariable { get; set; } = "OPENAI_API_KEY";

    public bool ShareSystemMetricsWithAi { get; set; }

    public bool VisionEnabled { get; set; } = true;

    public bool StartWithWindows { get; set; }

    public bool MinimizeToTray { get; set; } = true;

    public bool ShowWindowsNotifications { get; set; } = true;

    public bool PlayNotificationSounds { get; set; } = true;

    public bool HasCompletedOnboarding { get; set; }

    public bool ResourceGovernorEnabled { get; set; } = true;

    public bool PauseWakeWordInGameMode { get; set; } = true;

    public bool ProtectVisionWhenBusy { get; set; } = true;

    public HardwarePerformanceMode HardwarePerformanceMode { get; set; } = HardwarePerformanceMode.Automatic;

    // ---------- Diseño D6 (Fase 3 — Kohana Flow) ----------

    /// <summary>
    /// Registra el atajo global de dictado. Activado por omisión, igual que el resto de atajos
    /// globales ya existentes (Alt+A, Ctrl+Espacio…): el micrófono solo graba mientras el dictado
    /// está explícitamente en curso, nunca de fondo.
    /// </summary>
    public bool FlowEnabled { get; set; } = true;

    public FlowMode FlowMode { get; set; } = FlowMode.Texto;

    /// <summary>
    /// Diccionario personal, en formato "dicho=escrito" por línea. Todavía no hay interfaz para
    /// editarlo: se lee del archivo de ajustes si está presente. Ver la nota de pendientes de D6.3.
    /// </summary>
    public List<string> FlowDictionary { get; set; } = [];

    /// <summary>Atajos personales, en el mismo formato "dicho=escrito".</summary>
    public List<string> FlowSnippets { get; set; } = [];

    /// <summary>
    /// Diseño D9 (Fase 6) — controles de la memoria. Objeto propio en vez de campos sueltos porque
    /// la política que decide qué se recuerda necesita verlos juntos.
    /// </summary>
    public MemorySettings Memory { get; set; } = new();

    /// <summary>
    /// Diseño D12 (Fase 5) — la carpeta de proyecto autorizada. Vacía por omisión: Kohana no tiene
    /// acceso a ninguna hasta que la persona elige una.
    /// </summary>
    public WorkspaceSettings Workspace { get; set; } = new();

    /// <summary>
    /// Diseño D16 — permisos por capacidad. Objeto propio, como la memoria y el proyecto, porque el
    /// broker necesita verlos juntos para decidir.
    /// </summary>
    public PermissionSettings Permissions { get; set; } = new();

    /// <summary>
    /// Diseño D18 — hasta dónde puede llegar Kohana al actuar sobre el equipo. **Campo propio, no el
    /// del proyecto**: son capacidades distintas y el modelo de confianza exige que cada permiso sea
    /// independiente. Compartir el nivel haría que subir el del proyecto concediera también el de
    /// Computer Use, que es justo lo que "habilitar Lens no habilita Computer Use" prohíbe.
    /// </summary>
    public AutonomyLevel ComputerUseAutonomyLevel { get; set; } = AutonomyLevel.Proponer;

    public void Normalize()
    {
        if (SchemaVersion < 2)
        {
            ShowGpuInPeek = true;
            ShowDiskInPeek = false;
            SchemaVersion = 2;
        }

        if (SchemaVersion < 3)
        {
            RecentConversationMessageLimit = 8;
            SchemaVersion = 3;
        }

        if (SchemaVersion < 4)
        {
            SpeakVoiceResponses = false;
            SchemaVersion = 4;
        }

        if (SchemaVersion < 5)
        {
            WakeWordEnabled = false;
            WakeWordPhrase = WakePhrase.OyeKohana;
            SchemaVersion = 5;
        }

        if (SchemaVersion < 6)
        {
            AiProvider = AiProviderKind.Disabled;
            AiBaseUrl = string.Empty;
            AiModel = string.Empty;
            AiApiKeyEnvironmentVariable = "OPENAI_API_KEY";
            ShareSystemMetricsWithAi = false;
            SchemaVersion = 6;
        }

        if (SchemaVersion < 7)
        {
            VoiceInputDeviceNumber = -1;
            SchemaVersion = 7;
        }

        if (SchemaVersion < 8)
        {
            VisionEnabled = true;
            SchemaVersion = 8;
        }

        if (SchemaVersion < 9)
        {
            StartWithWindows = false;
            MinimizeToTray = true;
            ShowWindowsNotifications = true;
            PlayNotificationSounds = true;
            SchemaVersion = 9;
        }

        if (SchemaVersion < 10)
        {
            HasCompletedOnboarding = false;
            SchemaVersion = 10;
        }

        if (SchemaVersion < 11)
        {
            Width = Math.Max(Width, 650);
            SchemaVersion = 11;
        }

        if (SchemaVersion < 12)
        {
            Width = Math.Max(Width, 700);
            SchemaVersion = 12;
        }

        if (SchemaVersion < 13)
        {
            ResourceGovernorEnabled = true;
            PauseWakeWordInGameMode = true;
            ProtectVisionWhenBusy = true;
            SchemaVersion = 13;
        }

        if (SchemaVersion < 14)
        {
            // La etapa Kohana conserva compatibilidad con archivos antiguos,
            // pero recomienda una frase más distintiva para reducir activaciones accidentales.
            if (WakeWordPhrase.IsLegacy())
            {
                WakeWordPhrase = WakePhrase.OyeKohana;
            }

            if (string.Equals(AccentColor, "#8B6CFF", StringComparison.OrdinalIgnoreCase))
            {
                AccentColor = "#E98AAF";
            }

            SchemaVersion = 14;
        }

        if (SchemaVersion < 15)
        {
            SideRailExpanded = false;
            WakeWordSensitivity = WakeSensitivity.Balanced;
            SchemaVersion = 15;
        }

        if (SchemaVersion < 16)
        {
            // Los archivos antiguos pueden no incluir esta propiedad, pero una
            // actualización no debe borrar aliases que ya estén presentes.
            WakeWordAliases ??= [];
            SchemaVersion = 16;
        }

        if (SchemaVersion < 17)
        {
            // Los archivos anteriores a la Fase 2.2 no conocen el modo de rendimiento
            // adaptativo; Automatic es el valor neutro que no cambia ningún motor.
            HardwarePerformanceMode = HardwarePerformanceMode.Automatic;
            SchemaVersion = 17;
        }

        if (SchemaVersion < 18)
        {
            // Diseño D6 (Fase 3 — Kohana Flow). Igual que el escalón v16 con los aliases: un
            // archivo antiguo no trae estas listas, pero migrar no debe borrar las que ya existan.
            FlowDictionary ??= [];
            FlowSnippets ??= [];
            SchemaVersion = 18;
        }

        if (SchemaVersion < 19)
        {
            // Diseño D9 — la memoria llega apagada para todo el mundo, también al actualizar: el
            // roadmap dice que nunca se activa por defecto, y una migración no es un consentimiento.
            Memory ??= new MemorySettings();
            Memory.Enabled = false;
            SchemaVersion = 19;
        }

        if (SchemaVersion < 20)
        {
            // Diseño D12 — al actualizar nadie hereda una carpeta autorizada. Igual que con la
            // memoria en v19: una migración no es un consentimiento, y el acceso a los archivos de
            // un proyecto es de los permisos más altos que Kohana puede pedir.
            Workspace ??= new WorkspaceSettings();
            Workspace.Revoke();
            SchemaVersion = 20;
        }

        if (SchemaVersion < 21)
        {
            // Diseño D16 — al actualizar, los permisos llegan con su valor conservador: nada de
            // Computer Use, y todo lo demás en "preguntar". Igual que en v19 y v20, una migración no
            // es un consentimiento, y éste es el permiso más alto del roadmap.
            Permissions = new PermissionSettings();
            SchemaVersion = 21;
        }

        if (SchemaVersion < 22)
        {
            // Diseño D18 — un archivo anterior no trae este campo, y sin escalón se quedaría en 0,
            // que no es ningún nivel válido. Se pone en "Proponer", que es proponer sin ejecutar.
            ComputerUseAutonomyLevel = AutonomyLevel.Proponer;
            SchemaVersion = 22;
        }

        if (SchemaVersion < 23)
        {
            // Diseño D25 — "Ollama" se parte en dos: el motor que Kohana administra
            // (AiProviderKind.KohanaLocal, puerto 11435) y el que la persona instaló por su cuenta
            // (AiProviderKind.Ollama, puerto 11434). Quien ya apuntaba al administrado se mueve al
            // proveedor nuevo; si no, se queda donde estaba. Esta migración sí conserva una elección
            // previa en vez de reimponer un valor por omisión: no concede nada que no estuviera ya
            // concedido, solo le pone el nombre correcto a lo que la persona ya eligió.
            if (AiProvider == AiProviderKind.Ollama &&
                OllamaRuntimeEndpoints.IsManagedBaseUrl(AiBaseUrl))
            {
                AiProvider = AiProviderKind.KohanaLocal;
            }

            SchemaVersion = 23;
        }

        if (SchemaVersion < 24)
        {
            // Diseño D27 — la revelación por borde llega activada también al actualizar. A
            // diferencia de la memoria (v19) o los permisos (v21), aquí no se está concediendo
            // ningún acceso: es una forma más de abrir una ventana que ya se podía abrir con Alt+A.
            EdgeRevealEnabled = true;
            SchemaVersion = 24;
        }

        if (SchemaVersion < 26)
        {
            // Diseño D58 — el modelo que ya estuviera elegido se apunta como el de su proveedor, en
            // vez de empezar la memoria vacía. Quien tenía un modelo afinado lo conserva al primer
            // cambio de proveedor y vuelta, que es justo cuando se notaría la pérdida.
            AiModelsByProvider ??= [];

            if (AiProvider != AiProviderKind.Disabled && !string.IsNullOrWhiteSpace(AiModel))
            {
                AiModelsByProvider[AiProvider.ToString()] = AiModel.Trim();
            }

            SchemaVersion = 26;
        }

        if (SchemaVersion < 25)
        {
            // Diseño D52 — el rail se reduce a Chat, Sistema y Personalizar también al actualizar.
            //
            // Esta migración SÍ reimpone valores por omisión sobre una elección previa, y hay que
            // decirlo: quien tuviera Audio o Captura visibles se los va a encontrar fuera del rail.
            // Se hace igualmente porque el cambio es el reparto de la pantalla, no un permiso —
            // nada deja de estar disponible, todo sigue en la paleta y vuelve con una casilla— y
            // porque dejar el rail viejo en los equipos ya instalados significaría que la
            // reorganización solo la vería quien instalara de cero.
            ShowHomeModule = false;
            ShowTasksModule = false;
            ShowFocusModule = false;
            ShowRoutinesModule = false;
            ShowAudioModule = false;
            ShowCaptureModule = false;
            ShowSystemModule = true;
            SchemaVersion = CurrentSchemaVersion;
        }

        Width = Math.Clamp(Width, 680, 820);
        Opacity = Math.Clamp(Opacity, 0.82, 1.0);
        RecentConversationMessageLimit = SaveConversationHistory
            ? Math.Clamp(RecentConversationMessageLimit, 8, 30)
            : 8;
        VoiceInputDeviceNumber = Math.Max(-1, VoiceInputDeviceNumber);

        if (!Enum.IsDefined(WakeWordPhrase))
        {
            WakeWordPhrase = WakePhrase.OyeKohana;
        }

        if (!Enum.IsDefined(WakeWordSensitivity))
        {
            WakeWordSensitivity = WakeSensitivity.Balanced;
        }

        WakeWordAliases = WakeWordAliasPolicy.NormalizeMany(WakeWordAliases);

        // Diseño D6 — las listas pueden faltar en un settings.json escrito a mano o anterior a v18.
        FlowDictionary ??= [];
        FlowSnippets ??= [];

        Memory ??= new MemorySettings();
        Memory.Normalize();

        Workspace ??= new WorkspaceSettings();
        Workspace.Normalize();

        Permissions ??= new PermissionSettings();
        Permissions.Normalize();

        // Un settings.json escrito a mano no puede conceder un nivel que la política no ofrece: se
        // cae al valor por omisión en vez de aceptarlo. La autonomía se concede, no se hereda de un
        // archivo.
        if (!ComputerUse.ComputerUseAutonomyPolicy.IsAvailable(ComputerUseAutonomyLevel))
        {
            ComputerUseAutonomyLevel = ComputerUse.ComputerUseAutonomyPolicy.Default;
        }

        if (!Enum.IsDefined(FlowMode))
        {
            FlowMode = FlowMode.Texto;
        }

        if (!Enum.IsDefined(AiProvider))
        {
            AiProvider = AiProviderKind.Disabled;
        }

        var aiDefaults = AiProviderDefaults.Get(AiProvider);
        AiBaseUrl = AiProviderDefaults.NormalizeBaseUrl(AiBaseUrl);
        if (AiProvider != AiProviderKind.Disabled && string.IsNullOrWhiteSpace(AiBaseUrl))
        {
            AiBaseUrl = aiDefaults.BaseUrl;
        }

        // El motor administrado solo existe en un sitio, y Kohana es quien lo pone ahí. Dejar que
        // la dirección se desvíe es exactamente el fallo que costó una tarde: el modelo descargado
        // en 11435 y la aplicación preguntando en 11434. Aquí no hay nada que respetar del archivo.
        if (AiProviderDefaults.IsManagedByKohana(AiProvider))
        {
            AiBaseUrl = OllamaRuntimeEndpoints.ManagedBaseUrl;
        }

        AiModel = (AiModel ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(AiModel) &&
            !string.IsNullOrWhiteSpace(aiDefaults.DefaultModel))
        {
            AiModel = aiDefaults.DefaultModel;
        }

        AiApiKeyEnvironmentVariable = (AiApiKeyEnvironmentVariable ?? string.Empty).Trim();
        if (aiDefaults.RequiresApiKey &&
            string.IsNullOrWhiteSpace(AiApiKeyEnvironmentVariable))
        {
            AiApiKeyEnvironmentVariable = aiDefaults.ApiKeyEnvironmentVariable;
        }

        if (string.IsNullOrWhiteSpace(AccentColor))
        {
            AccentColor = "#E98AAF";
        }

        if (!Enum.IsDefined(HardwarePerformanceMode))
        {
            HardwarePerformanceMode = HardwarePerformanceMode.Automatic;
        }
    }

    /// <summary>
    /// Diseño D2 — devuelve a sus valores por defecto **solo** las preferencias visuales del
    /// shell: posición, ancho, opacidad, color de acento, animaciones y estado de la barra
    /// lateral.
    ///
    /// Todo lo demás se conserva intacto a propósito. Restaurar la apariencia no debe borrar
    /// tareas, rutinas, historial, ni la configuración de voz, IA, motores, integración con
    /// Windows o vista rápida: son datos y ajustes funcionales que el usuario configuró aparte y
    /// que no tienen nada que ver con cómo se ve Kohana. Esa separación es justamente lo que hace
    /// segura la acción "restaurar valores por defecto" de Personalizar.
    /// </summary>
    public void ResetVisualPreferences()
    {
        var defaults = new ShellPreferences();

        Position = defaults.Position;
        Width = defaults.Width;
        Opacity = defaults.Opacity;
        AccentColor = defaults.AccentColor;
        AccentSource = defaults.AccentSource;
        AnimationsEnabled = defaults.AnimationsEnabled;
        SideRailExpanded = defaults.SideRailExpanded;

        // Deliberadamente NO se llama a Normalize(): esa función arrastra la escalera de
        // migración por SchemaVersion, y con una versión antigua reasignaría valores
        // funcionales (proveedor de IA, palabra de activación, límites de conversación…). Es
        // decir, restaurar la apariencia podría borrar configuración que el usuario no pidió
        // tocar — justo lo que esta acción debe garantizar que no pasa. Los valores que se
        // acaban de asignar vienen de los propios valores por defecto, así que ya son válidos y
        // no necesitan normalizarse.
    }
}
