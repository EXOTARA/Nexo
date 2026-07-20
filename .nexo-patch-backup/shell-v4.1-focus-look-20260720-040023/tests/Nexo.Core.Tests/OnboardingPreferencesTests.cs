using Nexo.Core.Settings;

namespace Nexo.Core.Tests;

public sealed class OnboardingPreferencesTests
{
    [Fact]
    public void Normalize_MigratesSchemaNineToPendingOnboarding()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 9,
            HasCompletedOnboarding = true
        };

        preferences.Normalize();

        Assert.Equal(11, preferences.SchemaVersion);
        Assert.False(preferences.HasCompletedOnboarding);
    }

    [Fact]
    public void Normalize_PreservesCompletedOnboardingInCurrentSchema()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 10,
            HasCompletedOnboarding = true
        };

        preferences.Normalize();

        Assert.True(preferences.HasCompletedOnboarding);
    }
}
