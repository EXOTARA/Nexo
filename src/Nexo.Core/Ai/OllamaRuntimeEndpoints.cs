namespace Nexo.Core.Ai;

public static class OllamaRuntimeEndpoints
{
    public const string ExternalBaseUrl = "http://localhost:11434/v1";
    public const string ExternalTagsEndpoint = "http://localhost:11434/api/tags";
    public const string ManagedBaseUrl = "http://localhost:11435/v1";
    public const string ManagedTagsEndpoint = "http://localhost:11435/api/tags";

    public static bool IsManagedBaseUrl(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) ||
            !Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!uri.IsLoopback || uri.Port != 11435)
        {
            return false;
        }

        var path = uri.AbsolutePath.TrimEnd('/');
        return path.Length == 0 ||
               path.Equals("/v1", StringComparison.OrdinalIgnoreCase);
    }
}
