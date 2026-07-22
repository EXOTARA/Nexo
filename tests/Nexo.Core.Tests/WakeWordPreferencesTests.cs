using Nexo.Core.Settings;
using Nexo.Core.Voice;

namespace Nexo.Core.Tests;

public sealed class WakeWordPreferencesTests
{
    [Fact]
    public void Normalize_MigratesVeryOldWakeWordAsDisabled()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 4,
            WakeWordEnabled = true,
            WakeWordPhrase = WakeWordPhrase.OyeNexo
        };

        preferences.Normalize();

        Assert.Equal(14, preferences.SchemaVersion);
        Assert.False(preferences.WakeWordEnabled);
        Assert.Equal(WakeWordPhrase.OyeKohana, preferences.WakeWordPhrase);
    }

    [Theory]
    [InlineData(WakeWordPhrase.Nexo)]
    [InlineData(WakeWordPhrase.OyeNexo)]
    [InlineData(WakeWordPhrase.HeyNexo)]
    public void Normalize_MigratesLegacyBrandPhrase(WakeWordPhrase legacyPhrase)
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 13,
            WakeWordEnabled = true,
            WakeWordPhrase = legacyPhrase
        };

        preferences.Normalize();

        Assert.True(preferences.WakeWordEnabled);
        Assert.Equal(14, preferences.SchemaVersion);
        Assert.Equal(WakeWordPhrase.OyeKohana, preferences.WakeWordPhrase);
    }

    [Fact]
    public void Normalize_PreservesCurrentKohanaPreference()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 14,
            WakeWordEnabled = true,
            WakeWordPhrase = WakeWordPhrase.Kohana
        };

        preferences.Normalize();

        Assert.True(preferences.WakeWordEnabled);
        Assert.Equal(WakeWordPhrase.Kohana, preferences.WakeWordPhrase);
    }

    [Fact]
    public void Normalize_RepairsUnknownWakeWordPhrase()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 14,
            WakeWordPhrase = (WakeWordPhrase)99
        };

        preferences.Normalize();

        Assert.Equal(WakeWordPhrase.OyeKohana, preferences.WakeWordPhrase);
    }
}
