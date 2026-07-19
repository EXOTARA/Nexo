using Microsoft.Win32;
using Nexo.Core.WindowsIntegration;

namespace Nexo.Windows.WindowsIntegration;

public sealed class WindowsStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Nexo";

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string value &&
                   !string.IsNullOrWhiteSpace(value);
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (System.Security.SecurityException)
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
                return StartupRegistrationResult.Completed(
                    "Nexo ya no se iniciará con Windows.");
            }

            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                return StartupRegistrationResult.Failed(
                    "No pude localizar el ejecutable actual de Nexo.");
            }

            key.SetValue(
                ValueName,
                StartupCommandBuilder.Build(executablePath),
                RegistryValueKind.String);

            return StartupRegistrationResult.Completed(
                "Nexo se iniciará en segundo plano cuando abras sesión en Windows.");
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
}

public sealed record StartupRegistrationResult(bool Success, string Message)
{
    public static StartupRegistrationResult Completed(string message) =>
        new(true, message);

    public static StartupRegistrationResult Failed(string message) =>
        new(false, message);
}
