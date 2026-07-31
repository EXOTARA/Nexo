using Nexo.Core.Ai;
using Nexo.Core.AdaptiveEngine;
using Nexo.Core.Flow;
using Nexo.Core.Voice;
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
    public int SchemaVersion { get; set; }
    public SidebarPosition Position { get; set; } = SidebarPosition.Right;

    public double Width { get; set; } = 700;

    public double Opacity { get; set; } = 0.96;

    public string AccentColor { get; set; } = "#E98AAF";

    public bool AnimationsEnabled { get; set; } = true;

    public bool SideRailExpanded { get; set; }

    public bool ShowAudioModule { get; set; } = true;

    public bool ShowCaptureModule { get; set; } = true;

    public bool ShowSystemModule { get; set; } = true;

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

        AiModel = (AiModel ?? string.Empty).Trim();
        if (AiProvider == AiProviderKind.OpenAI && string.IsNullOrWhiteSpace(AiModel))
        {
            AiModel = aiDefaults.DefaultModel;
        }

        AiApiKeyEnvironmentVariable = (AiApiKeyEnvironmentVariable ?? string.Empty).Trim();
        if (AiProvider == AiProviderKind.OpenAI &&
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
