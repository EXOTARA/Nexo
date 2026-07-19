using Nexo.Core.Settings;

namespace Nexo.Core.Tests;

public sealed class VisionPreferencesTests
{
    [Fact]
    public void Normalize_EnablesOnDemandVisionByDefault()
    {
        var preferences = new ShellPreferences();

        preferences.Normalize();

        Assert.True(preferences.VisionEnabled);
        Assert.Equal(9, preferences.SchemaVersion);
    }

    [Fact]
    public void Normalize_PreservesDisabledVisionOnCurrentSchema()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 9,
            VisionEnabled = false
        };

        preferences.Normalize();

        Assert.False(preferences.VisionEnabled);
    }

    [Fact]
    public void Normalize_InitializesCustomVisionExclusions()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 8,
            VisionCustomExclusions = null!
        };

        preferences.Normalize();

        Assert.Equal(9, preferences.SchemaVersion);
        Assert.Equal(string.Empty, preferences.VisionCustomExclusions);
    }
}
