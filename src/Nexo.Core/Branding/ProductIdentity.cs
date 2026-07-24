namespace Nexo.Core.Branding;

/// <summary>
/// Identidad pública de la aplicación. Los namespaces internos permanecen como
/// Nexo durante la transición para reducir el riesgo del cambio de marca.
/// </summary>
public static class ProductIdentity
{
    public const string ProductName = "Kohana";
    public const string PreviousProductName = "Nexo";
    public const string Tagline = "Tu Windows, en flor.";
    public const string AssistantDescription = "agente personal para Windows";
    public const string DataDirectoryName = "Kohana";
    public const string LegacyDataDirectoryName = "Nexo";
    public const string ExecutableName = "Kohana.exe";
    public const string LegacyExecutableName = "Nexo.exe";
    public const string DefaultWakePhrase = "Oye Kohana";
    public const string ShortWakePhrase = "Kohana";
    public const string RepositoryOwner = "EXOTARA";
    public const string RepositoryName = "Nexo";
    public const string RepositoryUrl = "https://github.com/EXOTARA/Nexo";
    public const string SupportName = "Kohana Support";

    public static string DisplayNameWithTagline => $"{ProductName} — {Tagline}";
}
