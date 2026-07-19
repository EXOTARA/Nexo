using System.Net.Http.Json;
using System.Text.Json;
using Nexo.Core.Ai;

namespace Nexo.Windows.Ai;

public sealed class OllamaModelService : IOllamaModelService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    public OllamaModelService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        _ownsClient = httpClient is null;
    }

    public async Task<IReadOnlyList<OllamaModelInfo>> ListAsync(
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CreateTimeout(cancellationToken, TimeSpan.FromSeconds(20));
        using var response = await _httpClient.GetAsync(
            BuildEndpoint(baseUrl, "tags"),
            timeout.Token);
        var body = await response.Content.ReadAsStringAsync(timeout.Token);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("models", out var models) ||
            models.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<OllamaModelInfo>();
        foreach (var item in models.EnumerateArray())
        {
            var name = ReadString(item, "name") ?? ReadString(item, "model");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var size = item.TryGetProperty("size", out var sizeElement) &&
                       sizeElement.TryGetInt64(out var sizeValue)
                ? sizeValue
                : 0L;

            DateTimeOffset? modifiedAt = null;
            var modifiedText = ReadString(item, "modified_at");
            if (DateTimeOffset.TryParse(modifiedText, out var parsedDate))
            {
                modifiedAt = parsedDate;
            }

            result.Add(new OllamaModelInfo(name.Trim(), size, modifiedAt));
        }

        return result
            .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<OllamaOperationResult> PullAsync(
        string baseUrl,
        string model,
        IProgress<OllamaPullProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedModel = OllamaModelName.Normalize(model);
        if (normalizedModel is null)
        {
            return OllamaOperationResult.Failed("Escribe un nombre de modelo válido.");
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                BuildEndpoint(baseUrl, "pull"))
            {
                Content = JsonContent.Create(new
                {
                    model = normalizedModel,
                    stream = true
                })
            };

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(cancellationToken);
                return OllamaOperationResult.Failed(
                    $"Ollama no pudo descargar el modelo: {Summarize(detail)}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);
            string? lastStatus = null;

            while (true)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (root.TryGetProperty("error", out var errorElement) &&
                    errorElement.ValueKind == JsonValueKind.String)
                {
                    return OllamaOperationResult.Failed(
                        errorElement.GetString() ?? "Ollama informó un error desconocido.");
                }

                lastStatus = ReadString(root, "status") ?? lastStatus;
                long? completed = TryReadInt64(root, "completed");
                long? total = TryReadInt64(root, "total");
                progress?.Report(new OllamaPullProgress(
                    lastStatus ?? "Descargando modelo…",
                    completed,
                    total));
            }

            return OllamaOperationResult.Completed(
                $"El modelo {normalizedModel} quedó instalado.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return OllamaOperationResult.Failed("La descarga tardó demasiado.");
        }
        catch (OperationCanceledException)
        {
            return OllamaOperationResult.Failed("La descarga fue cancelada.");
        }
        catch (HttpRequestException exception)
        {
            return OllamaOperationResult.Failed(
                $"No pude conectar con Ollama: {exception.Message}");
        }
        catch (JsonException)
        {
            return OllamaOperationResult.Failed(
                "Ollama respondió con un progreso que Nexo no pudo interpretar.");
        }
        catch (Exception exception)
        {
            return OllamaOperationResult.Failed(
                $"No pude descargar el modelo: {exception.Message}");
        }
    }

    public async Task<OllamaOperationResult> DeleteAsync(
        string baseUrl,
        string model,
        CancellationToken cancellationToken = default)
    {
        var normalizedModel = OllamaModelName.Normalize(model);
        if (normalizedModel is null)
        {
            return OllamaOperationResult.Failed("Selecciona un modelo válido.");
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Delete,
                BuildEndpoint(baseUrl, "delete"))
            {
                Content = JsonContent.Create(new { model = normalizedModel })
            };
            using var timeout = CreateTimeout(cancellationToken, TimeSpan.FromSeconds(45));
            using var response = await _httpClient.SendAsync(request, timeout.Token);

            if (response.IsSuccessStatusCode)
            {
                return OllamaOperationResult.Completed(
                    $"El modelo {normalizedModel} fue eliminado.");
            }

            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            return OllamaOperationResult.Failed(
                $"No pude eliminar el modelo: {Summarize(body)}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return OllamaOperationResult.Failed("Ollama tardó demasiado en eliminar el modelo.");
        }
        catch (HttpRequestException exception)
        {
            return OllamaOperationResult.Failed(
                $"No pude conectar con Ollama: {exception.Message}");
        }
        catch (Exception exception)
        {
            return OllamaOperationResult.Failed(
                $"No pude eliminar el modelo: {exception.Message}");
        }
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }

    private static Uri BuildEndpoint(string baseUrl, string relativePath)
    {
        var normalized = AiProviderDefaults.NormalizeBaseUrl(baseUrl);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "http://localhost:11434";
        }

        if (normalized.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^3];
        }

        if (normalized.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^4];
        }

        return new Uri(
            normalized.TrimEnd('/') + "/api/" + relativePath.TrimStart('/'),
            UriKind.Absolute);
    }

    private static CancellationTokenSource CreateTimeout(
        CancellationToken cancellationToken,
        TimeSpan duration)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(duration);
        return source;
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static long? TryReadInt64(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.TryGetInt64(out var value)
            ? value
            : null;

    private static string Summarize(string? text)
    {
        var compact = (text ?? string.Empty)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        if (compact.Length == 0)
        {
            return "respuesta vacía";
        }

        return compact.Length <= 220 ? compact : compact[..220] + "…";
    }
}
