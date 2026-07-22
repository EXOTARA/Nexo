using Nexo.Core.Ai;
using Nexo.Core.Voice;
using WakePhrase = Nexo.Core.Voice.WakeWordPhrase;

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
    }
}
