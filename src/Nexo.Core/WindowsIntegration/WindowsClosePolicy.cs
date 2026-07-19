namespace Nexo.Core.WindowsIntegration;

public static class WindowsClosePolicy
{
    public static bool ShouldHideInsteadOfClose(
        bool minimizeToTray,
        bool explicitExitRequested)
    {
        return minimizeToTray && !explicitExitRequested;
    }
}
