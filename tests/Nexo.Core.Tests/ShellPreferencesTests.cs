using Nexo.Core.Settings;

namespace Nexo.Core.Tests;

public sealed class ShellPreferencesTests
{
    [Fact]
    public void Normalize_ClampsWidthAndOpacity()
    {
        var preferences = new ShellPreferences
        {
            Width = 900,
            Opacity = 0.2
        };

        preferences.Normalize();

        Assert.Equal(820, preferences.Width);
        Assert.Equal(0.82, preferences.Opacity);
    }


    [Fact]
    public void Normalize_ProtectsReadableShellWidth()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 12,
            Width = 320
        };

        preferences.Normalize();

        // Diseño D58 — cambiada a propósito: el suelo bajó de 680 a 460. Lo que esta prueba
        // protege no es el número sino que un ancho absurdo se sube hasta uno legible, y eso sigue
        // igual; el suelo bajó porque a 680 no había forma de acercarse a una barra lateral de
        // verdad por mucho que se arrastrara el control.
        Assert.Equal(ShellPreferences.MinimumWidth, preferences.Width);
    }

    [Fact]
    public void Normalize_MigratesLegacyShellToWorkspaceWidth()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 10,
            Width = 500
        };

        preferences.Normalize();

        // Diseño D58 — cambiada a propósito: quien viene de un esquema antiguo aterriza ahora en
        // el ancho nuevo por omisión (560), no en el viejo (700). Es el mismo salto que hace la
        // migración v27 con quien nunca tocó el control.
        Assert.Equal(560, preferences.Width);
        Assert.Equal(ShellPreferences.CurrentSchemaVersion, preferences.SchemaVersion);
    }


    [Fact]
    public void Normalize_RestoresDefaultAccentWhenEmpty()
    {
        var preferences = new ShellPreferences
        {
            AccentColor = " "
        };

        preferences.Normalize();

        Assert.Equal("#E98AAF", preferences.AccentColor);
    }

    [Fact]
    public void Peek_UsesCpuMemoryAndGpuByDefault()
    {
        var preferences = new ShellPreferences();

        preferences.Normalize();

        Assert.True(preferences.PeekEnabled);
        Assert.True(preferences.ShowCpuInPeek);
        Assert.True(preferences.ShowMemoryInPeek);
        Assert.True(preferences.ShowGpuInPeek);
        Assert.False(preferences.ShowDiskInPeek);
        Assert.True(preferences.ShowTopProcessInPeek);
    }

[Fact]
public void ConversationHistory_IsPrivateAndLimitedByDefault()
{
    var preferences = new ShellPreferences();

    preferences.Normalize();

    Assert.False(preferences.SaveConversationHistory);
    Assert.Equal(8, preferences.RecentConversationMessageLimit);
    }

[Fact]
public void Normalize_ClampsConversationMessageLimit()
{
    var preferences = new ShellPreferences
    {
        SchemaVersion = 3,
        SaveConversationHistory = true,
        RecentConversationMessageLimit = 200
    };

    preferences.Normalize();

    Assert.True(preferences.SaveConversationHistory);
    Assert.Equal(30, preferences.RecentConversationMessageLimit);
    }
    [Fact]
    public void Normalize_EnablesResourceProtectionForLegacySettings()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 12,
            ResourceGovernorEnabled = false,
            PauseWakeWordInGameMode = false,
            ProtectVisionWhenBusy = false
        };

        preferences.Normalize();

        Assert.Equal(ShellPreferences.CurrentSchemaVersion, preferences.SchemaVersion);
        Assert.True(preferences.ResourceGovernorEnabled);
        Assert.True(preferences.PauseWakeWordInGameMode);
        Assert.True(preferences.ProtectVisionWhenBusy);
    }

    [Fact]
    public void Normalize_AddsShellAndWakeWordReliabilityDefaults()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 14,
            SideRailExpanded = true,
            WakeWordSensitivity = (Nexo.Core.Voice.WakeWordSensitivity)99
        };

        preferences.Normalize();

        Assert.Equal(ShellPreferences.CurrentSchemaVersion, preferences.SchemaVersion);
        Assert.False(preferences.SideRailExpanded);
        Assert.Equal(
            Nexo.Core.Voice.WakeWordSensitivity.Balanced,
            preferences.WakeWordSensitivity);
    }

    [Fact]
    public void Default_HardwarePerformanceModeIsAutomatic()
    {
        var preferences = new ShellPreferences();

        Assert.Equal(
            Nexo.Core.AdaptiveEngine.HardwarePerformanceMode.Automatic,
            preferences.HardwarePerformanceMode);
    }

    [Fact]
    public void Normalize_MigratesLegacyFileWithoutHardwarePerformanceMode()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 16
        };

        preferences.Normalize();

        Assert.Equal(ShellPreferences.CurrentSchemaVersion, preferences.SchemaVersion);
        Assert.Equal(
            Nexo.Core.AdaptiveEngine.HardwarePerformanceMode.Automatic,
            preferences.HardwarePerformanceMode);
    }

    [Fact]
    public void Normalize_InvalidHardwarePerformanceMode_FallsBackToAutomatic()
    {
        var preferences = new ShellPreferences
        {
            HardwarePerformanceMode = (Nexo.Core.AdaptiveEngine.HardwarePerformanceMode)99
        };

        preferences.Normalize();

        Assert.Equal(
            Nexo.Core.AdaptiveEngine.HardwarePerformanceMode.Automatic,
            preferences.HardwarePerformanceMode);
    }

    [Theory]
    [InlineData(Nexo.Core.AdaptiveEngine.HardwarePerformanceMode.Automatic)]
    [InlineData(Nexo.Core.AdaptiveEngine.HardwarePerformanceMode.Eco)]
    [InlineData(Nexo.Core.AdaptiveEngine.HardwarePerformanceMode.Balanced)]
    [InlineData(Nexo.Core.AdaptiveEngine.HardwarePerformanceMode.Maximum)]
    public void Normalize_PreservesAnExplicitlyChosenValidMode(
        Nexo.Core.AdaptiveEngine.HardwarePerformanceMode mode)
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 17,
            HardwarePerformanceMode = mode
        };

        preferences.Normalize();

        Assert.Equal(mode, preferences.HardwarePerformanceMode);
    }
}

