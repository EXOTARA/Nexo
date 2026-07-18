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

        Assert.Equal(520, preferences.Width);
        Assert.Equal(0.82, preferences.Opacity);
    }

    [Fact]
    public void Normalize_RestoresDefaultAccentWhenEmpty()
    {
        var preferences = new ShellPreferences
        {
            AccentColor = " "
        };

        preferences.Normalize();

        Assert.Equal("#8B6CFF", preferences.AccentColor);
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
}

