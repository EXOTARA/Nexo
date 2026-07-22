namespace Nexo.Core.Updates;

public sealed record UpdateCheckResult(
    bool Success,
    bool IsUpdateAvailable,
    string CurrentVersion,
    string LatestVersion,
    string ReleaseName,
    string ReleaseUrl,
    string Message)
{
    public static UpdateCheckResult Unavailable(
        string currentVersion,
        string message) =>
        new(false, false, currentVersion, string.Empty, string.Empty, string.Empty, message);

    public static UpdateCheckResult Current(
        string currentVersion,
        string latestVersion,
        string releaseName,
        string releaseUrl) =>
        new(
            true,
            false,
            currentVersion,
            latestVersion,
            releaseName,
            releaseUrl,
            "Ya tienes la versión más reciente de Kohana.");

    public static UpdateCheckResult Available(
        string currentVersion,
        string latestVersion,
        string releaseName,
        string releaseUrl) =>
        new(
            true,
            true,
            currentVersion,
            latestVersion,
            releaseName,
            releaseUrl,
            $"Hay una nueva versión disponible: {latestVersion}.");
}
