namespace Nexo.Core.WindowsIntegration;

public static class StartupCommandBuilder
{
    public const string BackgroundArgument = "--background";

    public static string Build(string executablePath, bool startHidden = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

        var normalizedPath = executablePath.Trim().Trim('"');
        if (normalizedPath.Length == 0)
        {
            throw new ArgumentException("La ruta del ejecutable no puede estar vacía.", nameof(executablePath));
        }

        return startHidden
            ? $"\"{normalizedPath}\" {BackgroundArgument}"
            : $"\"{normalizedPath}\"";
    }

    public static bool ShouldStartHidden(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return arguments.Any(argument =>
            string.Equals(argument, BackgroundArgument, StringComparison.OrdinalIgnoreCase));
    }
}
