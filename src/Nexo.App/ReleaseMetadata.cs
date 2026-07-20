using System.Reflection;

namespace Nexo.App;

public static class ReleaseMetadata
{
    public static string CurrentVersion
    {
        get
        {
            var assembly = typeof(App).Assembly;
            var informational = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informational))
            {
                var plus = informational.IndexOf('+');
                return plus >= 0 ? informational[..plus] : informational;
            }

            return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }
    }

    public static string RepositoryUrl =>
        typeof(App).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute =>
                string.Equals(
                    attribute.Key,
                    "RepositoryUrl",
                    StringComparison.OrdinalIgnoreCase))?
            .Value ?? string.Empty;
}
