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

        Assert.Equal(7, preferences.SchemaVersion);
        Assert.False(preferences.SpeakVoiceResponses);
    }

    [Fact]
    public void Normalize_PreservesVoiceResponsesOnCurrentSchema()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 5,
            SpeakVoiceResponses = true
        };

        preferences.Normalize();

        Assert.True(preferences.SpeakVoiceResponses);
    }


    [Fact]
    public void Normalize_MigratesMicrophoneSelectionToWindowsDefault()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 6,
            VoiceInputDeviceNumber = 8
        };

        preferences.Normalize();

        Assert.Equal(7, preferences.SchemaVersion);
        Assert.Equal(-1, preferences.VoiceInputDeviceNumber);
    }

    [Fact]
    public void Normalize_PreservesCurrentMicrophoneSelection()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 7,
            VoiceInputDeviceNumber = 2
        };

        preferences.Normalize();

        Assert.Equal(2, preferences.VoiceInputDeviceNumber);
    }
}
