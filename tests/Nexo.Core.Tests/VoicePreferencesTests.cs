using Nexo.Core.Settings;

namespace Nexo.Core.Tests;

public sealed class VoicePreferencesTests
{
    [Fact]
    public void Normalize_MigratesVoiceResponsesAsOptIn()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 3,
            SpeakVoiceResponses = true
        };

        preferences.Normalize();

        Assert.Equal(4, preferences.SchemaVersion);
        Assert.False(preferences.SpeakVoiceResponses);
    }

    [Fact]
    public void Normalize_PreservesVoiceResponsesOnCurrentSchema()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 4,
            SpeakVoiceResponses = true
        };

        preferences.Normalize();

        Assert.True(preferences.SpeakVoiceResponses);
    }
}
