using Nexo.Core.Settings;
using Nexo.Core.Voice;

namespace Nexo.Core.Tests;

public sealed class WakeWordPreferencesTests
{
    [Fact]
    public void Normalize_MigratesWakeWordAsDisabledByDefault()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 4,
            WakeWordEnabled = true,
            WakeWordPhrase = WakeWordPhrase.OyeNexo
        };

        preferences.Normalize();

        Assert.Equal(8, preferences.SchemaVersion);
        Assert.False(preferences.WakeWordEnabled);
        Assert.Equal(WakeWordPhrase.Nexo, preferences.WakeWordPhrase);
    }

    [Fact]
    public void Normalize_PreservesCurrentWakeWordPreference()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 5,
            WakeWordEnabled = true,
            WakeWordPhrase = WakeWordPhrase.OyeNexo
        };

        preferences.Normalize();

        Assert.True(preferences.WakeWordEnabled);
        Assert.Equal(WakeWordPhrase.OyeNexo, preferences.WakeWordPhrase);
    }

    [Fact]
    public void Normalize_RepairsUnknownWakeWordPhrase()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 5,
            WakeWordPhrase = (WakeWordPhrase)99
        };

        preferences.Normalize();

        Assert.Equal(WakeWordPhrase.Nexo, preferences.WakeWordPhrase);
    }
}
