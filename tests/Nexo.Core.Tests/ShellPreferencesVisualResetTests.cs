using Nexo.Core.AdaptiveEngine;
using Nexo.Core.Ai;
using Nexo.Core.Settings;
using Nexo.Core.Voice;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D2 — "Restaurar apariencia" debe devolver las preferencias visuales a sus valores
/// originales y, sobre todo, **no borrar nada más**. La mitad interesante de estas pruebas es la
/// segunda: que lo funcional sobreviva.
/// </summary>
public sealed class ShellPreferencesVisualResetTests
{
    /// <summary>Preferencias con todo cambiado respecto a los valores por defecto.</summary>
    private static ShellPreferences CreateFullyCustomized() => new()
    {
        // Visuales — deben restaurarse.
        Position = SidebarPosition.Left,
        Width = 810,
        Opacity = 0.85,
        AccentColor = "#35C58A",
        AnimationsEnabled = false,
        SideRailExpanded = true,

        // Funcionales — deben sobrevivir intactas.
        WakeWordEnabled = true,
        WakeWordPhrase = WakeWordPhrase.HeySakura,
        WakeWordSensitivity = WakeWordSensitivity.High,
        WakeWordAliases = ["kojana"],
        SpeakVoiceResponses = true,
        VoiceInputDeviceNumber = 3,
        AiProvider = AiProviderKind.Ollama,
        AiModel = "llama3",
        ShareSystemMetricsWithAi = true,
        VisionEnabled = false,
        HardwarePerformanceMode = HardwarePerformanceMode.Maximum,
        StartWithWindows = true,
        MinimizeToTray = false,
        ShowWindowsNotifications = false,
        PlayNotificationSounds = false,
        SaveConversationHistory = true,
        RecentConversationMessageLimit = 25,
        ShowAudioModule = false,
        ShowCaptureModule = false,
        ShowSystemModule = false,
        PeekEnabled = false,
        ShowDiskInPeek = true,
        ResourceGovernorEnabled = false,
        PauseWakeWordInGameMode = false,
        ProtectVisionWhenBusy = false,
        HasCompletedOnboarding = true
    };

    // ---------- Lo visual se restaura ----------

    [Fact]
    public void ResetVisualPreferences_RestoresEveryVisualPreference()
    {
        var defaults = new ShellPreferences();
        var preferences = CreateFullyCustomized();

        preferences.ResetVisualPreferences();

        Assert.Equal(defaults.Position, preferences.Position);
        Assert.Equal(defaults.Width, preferences.Width);
        Assert.Equal(defaults.Opacity, preferences.Opacity);
        Assert.Equal(defaults.AccentColor, preferences.AccentColor);
        Assert.Equal(defaults.AnimationsEnabled, preferences.AnimationsEnabled);
        Assert.Equal(defaults.SideRailExpanded, preferences.SideRailExpanded);
    }

    // ---------- Lo funcional sobrevive ----------

    [Fact]
    public void ResetVisualPreferences_KeepsVoiceConfiguration()
    {
        var preferences = CreateFullyCustomized();

        preferences.ResetVisualPreferences();

        Assert.True(preferences.WakeWordEnabled);
        Assert.Equal(WakeWordPhrase.HeySakura, preferences.WakeWordPhrase);
        Assert.Equal(WakeWordSensitivity.High, preferences.WakeWordSensitivity);
        Assert.Contains("kojana", preferences.WakeWordAliases);
        Assert.True(preferences.SpeakVoiceResponses);
        Assert.Equal(3, preferences.VoiceInputDeviceNumber);
    }

    [Fact]
    public void ResetVisualPreferences_KeepsAiConfiguration()
    {
        var preferences = CreateFullyCustomized();

        preferences.ResetVisualPreferences();

        Assert.Equal(AiProviderKind.Ollama, preferences.AiProvider);
        Assert.Equal("llama3", preferences.AiModel);
        Assert.True(preferences.ShareSystemMetricsWithAi);
        Assert.False(preferences.VisionEnabled);
    }

    [Fact]
    public void ResetVisualPreferences_KeepsEngineAndPerformanceConfiguration()
    {
        var preferences = CreateFullyCustomized();

        preferences.ResetVisualPreferences();

        Assert.Equal(HardwarePerformanceMode.Maximum, preferences.HardwarePerformanceMode);
        Assert.False(preferences.ResourceGovernorEnabled);
        Assert.False(preferences.PauseWakeWordInGameMode);
        Assert.False(preferences.ProtectVisionWhenBusy);
    }

    [Fact]
    public void ResetVisualPreferences_KeepsWindowsIntegrationConfiguration()
    {
        var preferences = CreateFullyCustomized();

        preferences.ResetVisualPreferences();

        Assert.True(preferences.StartWithWindows);
        Assert.False(preferences.MinimizeToTray);
        Assert.False(preferences.ShowWindowsNotifications);
        Assert.False(preferences.PlayNotificationSounds);
    }

    [Fact]
    public void ResetVisualPreferences_KeepsConversationAndModuleChoices()
    {
        var preferences = CreateFullyCustomized();

        preferences.ResetVisualPreferences();

        Assert.True(preferences.SaveConversationHistory);
        Assert.Equal(25, preferences.RecentConversationMessageLimit);
        Assert.False(preferences.ShowAudioModule);
        Assert.False(preferences.ShowCaptureModule);
        Assert.False(preferences.ShowSystemModule);
        Assert.False(preferences.PeekEnabled);
        Assert.True(preferences.ShowDiskInPeek);
    }

    [Fact]
    public void ResetVisualPreferences_DoesNotReopenOnboarding()
    {
        // Reabrir la configuración inicial sería exactamente el tipo de efecto colateral que esta
        // acción no debe tener.
        var preferences = CreateFullyCustomized();

        preferences.ResetVisualPreferences();

        Assert.True(preferences.HasCompletedOnboarding);
    }

    // ---------- Rangos y estabilidad ----------

    [Fact]
    public void ResetVisualPreferences_LeavesValuesInsideTheirValidRanges()
    {
        var preferences = CreateFullyCustomized();

        preferences.ResetVisualPreferences();

        Assert.InRange(preferences.Opacity, 0.82, 1.0);
        // Diseño D58 — el rango bajó su suelo a 460. La prueba sigue diciendo lo mismo: reiniciar
        // lo visual no puede dejar el ancho fuera de lo que se admite.
        Assert.InRange(
            preferences.Width, ShellPreferences.MinimumWidth, ShellPreferences.MaximumWidth);
        Assert.False(string.IsNullOrWhiteSpace(preferences.AccentColor));
    }

    [Fact]
    public void ResetVisualPreferences_IsIdempotent()
    {
        var once = CreateFullyCustomized();
        once.ResetVisualPreferences();

        var twice = CreateFullyCustomized();
        twice.ResetVisualPreferences();
        twice.ResetVisualPreferences();

        Assert.Equal(once.Width, twice.Width);
        Assert.Equal(once.Opacity, twice.Opacity);
        Assert.Equal(once.AccentColor, twice.AccentColor);
        Assert.Equal(once.Position, twice.Position);
        Assert.Equal(once.AnimationsEnabled, twice.AnimationsEnabled);
        Assert.Equal(once.SideRailExpanded, twice.SideRailExpanded);
    }

    [Fact]
    public void ResetVisualPreferences_KeepsSchemaVersionMigrated()
    {
        // Restaurar la apariencia no debe hacer retroceder la versión del esquema: eso volvería a
        // disparar migraciones ya aplicadas.
        var preferences = CreateFullyCustomized();
        preferences.Normalize();
        var migratedVersion = preferences.SchemaVersion;

        preferences.ResetVisualPreferences();

        Assert.Equal(migratedVersion, preferences.SchemaVersion);
    }
}
