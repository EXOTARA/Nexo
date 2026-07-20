using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nexo.Core.Updates;

namespace Nexo.Windows.Updates;

public sealed class GitHubUpdateService : IUpdateService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _repositoryApiUrl;
    private readonly bool _ownsHttpClient;

    public GitHubUpdateService(string repositoryUrl, HttpClient? httpClient = null)
    {
        _repositoryApiUrl = BuildRepositoryApiUrl(repositoryUrl);
        _httpClient = httpClient ?? new HttpClient();
        _ownsHttpClient = httpClient is null;

        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("Nexo", "0.9"));
        }

        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<UpdateCheckResult> CheckAsync(
        string currentVersion,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_repositoryApiUrl))
        {
            return UpdateCheckResult.Unavailable(
                currentVersion,
                "Esta compilación no tiene un repositorio de actualizaciones configurado.");
        }

        if (!ReleaseVersion.TryParse(currentVersion, out var parsedCurrent))
        {
            return UpdateCheckResult.Unavailable(
                currentVersion,
                "No pude interpretar la versión actual de Nexo.");
        }

        try
        {
            using var response = await _httpClient.GetAsync(
                $"{_repositoryApiUrl}/releases?per_page=20",
                cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var releases = await JsonSerializer.DeserializeAsync<List<ReleaseResponse>>(
                stream,
                cancellationToken: cancellationToken) ?? [];

            var candidate = releases
                .Where(release =>
                    !release.Draft &&
                    (parsedCurrent.IsPreRelease || !release.PreRelease))
                .Select(release => new
                {
                    Release = release,
                    Parsed = ReleaseVersion.TryParse(release.TagName, out var parsed)
                        ? parsed
                        : null
                })
                .Where(item => item.Parsed is not null)
                .OrderByDescending(item => item.Parsed)
                .FirstOrDefault();

            if (candidate?.Parsed is null)
            {
                return UpdateCheckResult.Unavailable(
                    currentVersion,
                    "El repositorio todavía no tiene una versión publicada que Nexo pueda comparar.");
            }

            var latest = candidate.Parsed.ToString();
            var releaseName = string.IsNullOrWhiteSpace(candidate.Release.Name)
                ? $"Nexo {latest}"
                : candidate.Release.Name;
            var releaseUrl = candidate.Release.HtmlUrl ?? string.Empty;

            return candidate.Parsed.CompareTo(parsedCurrent) > 0
                ? UpdateCheckResult.Available(currentVersion, latest, releaseName, releaseUrl)
                : UpdateCheckResult.Current(currentVersion, latest, releaseName, releaseUrl);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return UpdateCheckResult.Unavailable(
                currentVersion,
                "La comprobación de actualizaciones agotó el tiempo de espera.");
        }
        catch (HttpRequestException exception)
        {
            return UpdateCheckResult.Unavailable(
                currentVersion,
                $"No pude consultar las actualizaciones: {exception.Message}");
        }
        catch (JsonException exception)
        {
            return UpdateCheckResult.Unavailable(
                currentVersion,
                $"GitHub devolvió una respuesta que no pude interpretar: {exception.Message}");
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    internal static string BuildRepositoryApiUrl(string? repositoryUrl)
    {
        if (string.IsNullOrWhiteSpace(repositoryUrl) ||
            !Uri.TryCreate(repositoryUrl.Trim(), UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var segments = uri.AbsolutePath
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 2)
        {
            return string.Empty;
        }

        var owner = segments[0];
        var repository = segments[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? segments[1][..^4]
            : segments[1];

        return $"https://api.github.com/repos/{owner}/{repository}";
    }

    private sealed record ReleaseResponse(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("html_url")] string? HtmlUrl,
        [property: JsonPropertyName("draft")] bool Draft,
        [property: JsonPropertyName("prerelease")] bool PreRelease);
}
