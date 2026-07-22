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

        Assert.Equal(680, preferences.Width);
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

        Assert.Equal(700, preferences.Width);
        Assert.Equal(16, preferences.SchemaVersion);
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

        Assert.Equal(16, preferences.SchemaVersion);
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

        Assert.Equal(16, preferences.SchemaVersion);
        Assert.False(preferences.SideRailExpanded);
        Assert.Equal(
            Nexo.Core.Voice.WakeWordSensitivity.Balanced,
            preferences.WakeWordSensitivity);
    }

}

