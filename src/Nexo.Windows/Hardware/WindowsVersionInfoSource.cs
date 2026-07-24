using Microsoft.Win32;

namespace Nexo.Windows.Hardware;

public interface IWindowsVersionInfoSource
{
    string? ReadEdition();

    string? ReadVersion();
}

public sealed class RegistryWindowsVersionInfoSource : IWindowsVersionInfoSource
{
    public string? ReadEdition()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var productName = key?.GetValue("ProductName") as string;
            return string.IsNullOrWhiteSpace(productName) ? null : productName;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public string? ReadVersion()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var displayVersion = key?.GetValue("DisplayVersion") as string ?? key?.GetValue("ReleaseId") as string;
            var buildNumber = key?.GetValue("CurrentBuildNumber") as string;

            var versionParts = new[] { displayVersion, buildNumber is null ? null : $"Build {buildNumber}" }
                .Where(part => !string.IsNullOrWhiteSpace(part));
            var version = string.Join(" · ", versionParts);

            return string.IsNullOrWhiteSpace(version) ? null : version;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
