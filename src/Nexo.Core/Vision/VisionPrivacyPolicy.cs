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

    public static bool IsSensitive(string? title, string? processName)
    {
        var normalizedProcess = (processName ?? string.Empty).Trim().ToLowerInvariant();
        if (SensitiveProcessNames.Any(value =>
                normalizedProcess.Equals(value, StringComparison.OrdinalIgnoreCase) ||
                normalizedProcess.StartsWith(value + ".", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var normalizedTitle = (title ?? string.Empty).Trim().ToLowerInvariant();
        return SensitiveTitleFragments.Any(fragment =>
            normalizedTitle.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }
}
