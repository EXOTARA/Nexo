using Nexo.Core.Settings;
using Nexo.Core.WindowsIntegration;

namespace Nexo.Core.Tests;

public sealed class WindowsIntegrationTests
{
    [Fact]
    public void StartupCommand_QuotesExecutableAndStartsHidden()
    {
        var command = StartupCommandBuilder.Build(@"C:\Program Files\Nexo\Nexo.App.exe");

        Assert.Equal(
            "\"C:\\Program Files\\Nexo\\Nexo.App.exe\" --background",
            command);
    }

    [Fact]
    public void StartupCommand_CanOpenNormally()
    {
        var command = StartupCommandBuilder.Build(@"C:\Nexo\Nexo.exe", startHidden: false);

        Assert.Equal("\"C:\\Nexo\\Nexo.exe\"", command);
    }

    [Theory]
    [InlineData("--background")]
    [InlineData("--BACKGROUND")]
    public void StartupArguments_DetectBackgroundMode(string argument)
    {
        Assert.True(StartupCommandBuilder.ShouldStartHidden([argument]));
    }

    [Fact]
    public void StartupArguments_DefaultToVisible()
    {
        Assert.False(StartupCommandBuilder.ShouldStartHidden([]));
    }

    [Fact]
    public void ClosePolicy_HidesWhenTrayModeIsEnabled()
    {
        Assert.True(WindowsClosePolicy.ShouldHideInsteadOfClose(
            minimizeToTray: true,
            explicitExitRequested: false));
    }

    [Fact]
    public void ClosePolicy_AllowsExplicitExit()
    {
        Assert.False(WindowsClosePolicy.ShouldHideInsteadOfClose(
            minimizeToTray: true,
            explicitExitRequested: true));
    }

    [Fact]
    public void ClosePolicy_ClosesWhenTrayModeIsDisabled()
    {
        Assert.False(WindowsClosePolicy.ShouldHideInsteadOfClose(
            minimizeToTray: false,
            explicitExitRequested: false));
    }

    [Fact]
    public void WindowsIntegration_IsSafeByDefault()
    {
        var preferences = new ShellPreferences();

        preferences.Normalize();

        Assert.False(preferences.StartWithWindows);
        Assert.True(preferences.MinimizeToTray);
        Assert.True(preferences.ShowWindowsNotifications);
        Assert.True(preferences.PlayNotificationSounds);
        Assert.Equal(14, preferences.SchemaVersion);
    }

    [Fact]
    public void Normalize_MigratesWindowsIntegrationPreferences()
    {
        var preferences = new ShellPreferences
        {
            SchemaVersion = 8,
            StartWithWindows = true,
            MinimizeToTray = false,
            ShowWindowsNotifications = false,
            PlayNotificationSounds = false
        };

        preferences.Normalize();

        Assert.False(preferences.StartWithWindows);
        Assert.True(preferences.MinimizeToTray);
        Assert.True(preferences.ShowWindowsNotifications);
        Assert.True(preferences.PlayNotificationSounds);
        Assert.Equal(14, preferences.SchemaVersion);
    }
}
