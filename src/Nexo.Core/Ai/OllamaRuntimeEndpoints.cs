namespace Nexo.Core.Ai;

public static class OllamaRuntimeEndpoints
{
    public const string ExternalBaseUrl = "http://127.0.0.1:11434/v1";
    public const string ExternalTagsEndpoint = "http://127.0.0.1:11434/api/tags";
    public const string ManagedBaseUrl = "http://127.0.0.1:11435/v1";
    public const string ManagedTagsEndpoint = "http://127.0.0.1:11435/api/tags";

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
