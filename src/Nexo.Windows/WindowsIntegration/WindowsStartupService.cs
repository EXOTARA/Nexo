using Microsoft.Win32;
using Nexo.Core.Branding;
using Nexo.Core.WindowsIntegration;

namespace Nexo.Windows.WindowsIntegration;

public sealed class WindowsStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = ProductIdentity.ProductName;
    private const string LegacyValueName = ProductIdentity.PreviousProductName;

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (HasRegistration(key, ValueName))
            {
                return true;
            }

            if (!HasRegistration(key, LegacyValueName))
            {
                return false;
            }

            // Una instalación anterior puede seguir apuntando a Nexo.exe.
            // Al detectar esa entrada se reemplaza por el ejecutable actual.
            var executablePath = Environment.ProcessPath;
            if (key is not null &&
                !string.IsNullOrWhiteSpace(executablePath) &&
                File.Exists(executablePath))
            {
                key.SetValue(
                    ValueName,
                    StartupCommandBuilder.Build(executablePath),
                    RegistryValueKind.String);
                key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (System.Security.SecurityException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public StartupRegistrationResult SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                return StartupRegistrationResult.Failed(
                    "Windows no permitió abrir la configuración de inicio.");
            }

            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
                return StartupRegistrationResult.Completed(
                    $"{ProductIdentity.ProductName} ya no se iniciará con Windows.");
            }

            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                return StartupRegistrationResult.Failed(
                    $"No pude localizar el ejecutable actual de {ProductIdentity.ProductName}.");
            }

            key.SetValue(
                ValueName,
                StartupCommandBuilder.Build(executablePath),
                RegistryValueKind.String);
            key.DeleteValue(LegacyValueName, throwOnMissingValue: false);

            return StartupRegistrationResult.Completed(
                $"{ProductIdentity.ProductName} se iniciará en segundo plano cuando abras sesión en Windows.");
        }
        catch (UnauthorizedAccessException)
        {
            return StartupRegistrationResult.Failed(
                "Windows no permitió cambiar la configuración de inicio.");
        }
        catch (System.Security.SecurityException)
        {
            return StartupRegistrationResult.Failed(
                "La política de seguridad bloqueó el inicio automático.");
        }
        catch (IOException exception)
        {
            return StartupRegistrationResult.Failed(
                $"No pude actualizar el inicio automático: {exception.Message}");
        }
    }

    private static bool HasRegistration(RegistryKey? key, string valueName) =>
        key?.GetValue(valueName) is string value &&
        !string.IsNullOrWhiteSpace(value);
}

public sealed record StartupRegistrationResult(bool Success, string Message)
{
    public static StartupRegistrationResult Completed(string message) =>
        new(true, message);

    public static StartupRegistrationResult Failed(string message) =>
        new(false, message);
}
