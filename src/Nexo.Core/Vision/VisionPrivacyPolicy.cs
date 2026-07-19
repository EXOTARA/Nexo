namespace Nexo.Core.Vision;

public static class VisionPrivacyPolicy
{
    private static readonly string[] SensitiveProcessNames =
    [
        "1password",
        "bitwarden",
        "keepass",
        "keepassxc",
        "credentialuibroker",
        "consentux",
        "logonui",
        "lockapp"
    ];

    private static readonly string[] SensitiveTitleFragments =
    [
        "administrador de credenciales",
        "credential manager",
        "seguridad de windows",
        "windows security",
        "contraseña",
        "password vault",
        "1password",
        "bitwarden",
        "keepass"
    ];

    public static bool IsSensitive(string? title, string? processName) =>
        IsSensitive(title, processName, []);

    public static bool IsSensitive(
        string? title,
        string? processName,
        IEnumerable<string>? customExclusions)
    {
        var normalizedProcess = (processName ?? string.Empty).Trim().ToLowerInvariant();
        if (SensitiveProcessNames.Any(value =>
                normalizedProcess.Equals(value, StringComparison.OrdinalIgnoreCase) ||
                normalizedProcess.StartsWith(value + ".", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var normalizedTitle = (title ?? string.Empty).Trim().ToLowerInvariant();
        if (SensitiveTitleFragments.Any(fragment =>
                normalizedTitle.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return (customExclusions ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Any(value =>
                normalizedTitle.Contains(value, StringComparison.OrdinalIgnoreCase) ||
                normalizedProcess.Contains(value, StringComparison.OrdinalIgnoreCase));
    }
}
