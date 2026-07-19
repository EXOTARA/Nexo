using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Nexo.Core.Ai;
using Nexo.Core.Assistant;

namespace Nexo.Windows.Ai;

public sealed class OpenAiCompatibleChatService : IAiChatService, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    public OpenAiCompatibleChatService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _ownsClient = httpClient is null;
    }

    public async Task<AiConnectionResult> TestConnectionAsync(
        AiProviderConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateConfiguration(configuration);
        if (validation is not null)
        {
            return AiConnectionResult.Failed(validation);
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                BuildEndpoint(configuration.BaseUrl, "models"));
            ApplyAuthentication(request, configuration);

            using var timeout = CreateTimeout(cancellationToken, TimeSpan.FromSeconds(20));
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                return AiConnectionResult.Failed(
                    BuildHttpError(response.StatusCode, body));
            }

            var models = ParseModels(body);
            var detail = models.Count == 0
                ? $"{configuration.DisplayName} respondió, pero no informó modelos disponibles."
                : $"{configuration.DisplayName} conectado · {models.Count} modelo(s) disponible(s).";

            if (!string.IsNullOrWhiteSpace(configuration.Model) &&
                models.Count > 0 &&
                !models.Contains(configuration.Model, StringComparer.OrdinalIgnoreCase))
            {
                detail += $" El modelo “{configuration.Model}” no apareció en la lista.";
            }

            return AiConnectionResult.Success(detail, models);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AiConnectionResult.Failed(
                "La conexión tardó demasiado. Comprueba que el proveedor esté iniciado y que la URL sea correcta.");
        }
        catch (HttpRequestException exception)
        {
            return AiConnectionResult.Failed(
                $"No pude conectar con {configuration.DisplayName}: {exception.Message}");
        }
        catch (JsonException)
        {
            return AiConnectionResult.Failed(
                "El proveedor respondió, pero el formato de la lista de modelos no era compatible.");
        }
    }

    public async Task<AiChatResult> SendAsync(
        AiProviderConfiguration configuration,
        AiChatRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = ValidateConfiguration(configuration);
        if (validation is not null)
        {
            return AiChatResult.Failed(validation);
        }

        if (string.IsNullOrWhiteSpace(configuration.Model))
        {
            return AiChatResult.Failed(
                "Selecciona o escribe un modelo en Personalización → Inteligencia artificial.");
        }

        var messages = BuildMessages(request);
        if (messages.Count == 1)
        {
            return AiChatResult.Failed("No había una consulta para enviar al proveedor.");
        }

        var payload = new ChatCompletionRequest(
            configuration.Model.Trim(),
            messages,
            Stream: false);

        try
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                BuildEndpoint(configuration.BaseUrl, "chat/completions"))
            {
                Content = JsonContent.Create(payload, options: SerializerOptions)
            };
            ApplyAuthentication(httpRequest, configuration);

            using var timeout = CreateTimeout(cancellationToken, TimeSpan.FromSeconds(90));
            using var response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                return AiChatResult.Failed(BuildHttpError(response.StatusCode, body));
            }

            var text = ParseAssistantText(body);
            return string.IsNullOrWhiteSpace(text)
                ? AiChatResult.Failed("El proveedor respondió sin texto utilizable.")
                : AiChatResult.Success(text);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AiChatResult.Failed(
                "La respuesta tardó demasiado. Puedes intentar de nuevo o usar un modelo más ligero.");
        }
        catch (HttpRequestException exception)
        {
            return AiChatResult.Failed(
                $"No pude conectar con {configuration.DisplayName}: {exception.Message}");
        }
        catch (JsonException)
        {
            return AiChatResult.Failed(
                "El proveedor respondió con un formato que Nexo todavía no reconoce.");
        }
    }

    public async IAsyncEnumerable<string> StreamAsync(
        AiProviderConfiguration configuration,
        AiChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = ValidateConfiguration(configuration);
        if (validation is not null)
        {
            throw new AiChatStreamException(validation);
        }

        if (string.IsNullOrWhiteSpace(configuration.Model))
        {
            throw new AiChatStreamException(
                "Selecciona o escribe un modelo en Personalización → Inteligencia artificial.");
        }

        var messages = BuildMessages(request);
        if (messages.Count == 1)
        {
            throw new AiChatStreamException("No había una consulta para enviar al proveedor.");
        }

        var payload = new ChatCompletionRequest(
            configuration.Model.Trim(),
            messages,
            Stream: true);

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            BuildEndpoint(configuration.BaseUrl, "chat/completions"))
        {
            Content = JsonContent.Create(payload, options: SerializerOptions)
        };
        ApplyAuthentication(httpRequest, configuration);

        using var timeout = CreateTimeout(cancellationToken, TimeSpan.FromSeconds(90));
        using var response = await SendStreamingRequestAsync(
            httpRequest,
            configuration,
            cancellationToken,
            timeout.Token);

        var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        var isLineStream = mediaType.Contains("event-stream", StringComparison.OrdinalIgnoreCase) ||
                           mediaType.Contains("ndjson", StringComparison.OrdinalIgnoreCase);

        if (!isLineStream)
        {
            var body = await response.Content.ReadAsStringAsync(timeout.Token);
            var text = ParseAssistantText(body);
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new AiChatStreamException(
                    "El proveedor respondió sin texto utilizable.");
            }

            yield return text;
            yield break;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var reader = new StreamReader(stream);
        var emittedText = false;

        while (true)
        {
            var line = await reader.ReadLineAsync(timeout.Token);
            if (line is null)
            {
                break;
            }

            var payloadLine = line.Trim();
            if (payloadLine.Length == 0 || payloadLine.StartsWith(':'))
            {
                continue;
            }

            if (payloadLine.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                payloadLine = payloadLine[5..].Trim();
            }

            if (payloadLine.Equals("[DONE]", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            var delta = TryParseStreamingText(payloadLine);
            if (string.IsNullOrEmpty(delta))
            {
                continue;
            }

            emittedText = true;
            yield return delta;
        }

        if (!emittedText)
        {
            throw new AiChatStreamException(
                "El proveedor terminó la respuesta sin enviar texto utilizable.");
        }
    }

    private async Task<HttpResponseMessage> SendStreamingRequestAsync(
        HttpRequestMessage request,
        AiProviderConfiguration configuration,
        CancellationToken originalCancellationToken,
        CancellationToken timeoutToken)
    {
        try
        {
            var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutToken);

            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            var body = await response.Content.ReadAsStringAsync(timeoutToken);
            var detail = BuildHttpError(response.StatusCode, body);
            response.Dispose();
            throw new AiChatStreamException(detail);
        }
        catch (OperationCanceledException) when (!originalCancellationToken.IsCancellationRequested)
        {
            throw new AiChatStreamException(
                "La respuesta tardó demasiado. Puedes intentar de nuevo o usar un modelo más ligero.");
        }
        catch (HttpRequestException exception)
        {
            throw new AiChatStreamException(
                $"No pude conectar con {configuration.DisplayName}: {exception.Message}",
                exception);
        }
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }

    private static string? ValidateConfiguration(AiProviderConfiguration configuration)
    {
        if (!configuration.IsEnabled)
        {
            return "El proveedor de IA está desactivado.";
        }

        if (!Uri.TryCreate(
                AiProviderDefaults.NormalizeBaseUrl(configuration.BaseUrl),
                UriKind.Absolute,
                out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return "La URL del proveedor no es válida.";
        }

        if (configuration.RequiresApiKey &&
            string.IsNullOrWhiteSpace(configuration.ReadApiKey()))
        {
            var variable = string.IsNullOrWhiteSpace(configuration.ApiKeyEnvironmentVariable)
                ? "OPENAI_API_KEY"
                : configuration.ApiKeyEnvironmentVariable;
            return $"No encontré la variable de entorno {variable}. Nexo no guarda claves dentro del proyecto.";
        }

        return null;
    }

    private static List<ChatMessage> BuildMessages(AiChatRequest request)
    {
        var systemText = request.Instructions.Trim();
        if (!string.IsNullOrWhiteSpace(request.SystemContext))
        {
            systemText += $"\n\nContexto autorizado del equipo:\n{request.SystemContext.Trim()}";
        }

        var messages = new List<ChatMessage>
        {
            new("system", systemText)
        };

        foreach (var message in request.Messages
                     .Where(item => !string.IsNullOrWhiteSpace(item.Text))
                     .TakeLast(20))
        {
            messages.Add(new ChatMessage(
                message.Role == ConversationRole.User ? "user" : "assistant",
                message.Text.Trim()));
        }

        return messages;
    }

    private static void ApplyAuthentication(
        HttpRequestMessage request,
        AiProviderConfiguration configuration)
    {
        var apiKey = configuration.ReadApiKey();
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                apiKey.Trim());
        }
    }

    private static Uri BuildEndpoint(string baseUrl, string relativePath)
    {
        var normalized = AiProviderDefaults.NormalizeBaseUrl(baseUrl);
        var requestedSuffix = "/" + relativePath.TrimStart('/');

        if (normalized.EndsWith(requestedSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(normalized, UriKind.Absolute);
        }

        return new Uri(normalized + requestedSuffix, UriKind.Absolute);
    }

    private static CancellationTokenSource CreateTimeout(
        CancellationToken cancellationToken,
        TimeSpan duration)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(duration);
        return source;
    }

    private static IReadOnlyList<string> ParseModels(string body)
    {
        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return data.EnumerateArray()
            .Where(item => item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
            .Select(item => item.GetProperty("id").GetString())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string TryParseStreamingText(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0)
            {
                return string.Empty;
            }

            var choice = choices[0];
            if (choice.TryGetProperty("delta", out var delta) &&
                delta.ValueKind == JsonValueKind.Object &&
                delta.TryGetProperty("content", out var deltaContent))
            {
                return ReadContentText(deltaContent);
            }

            if (choice.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.Object &&
                message.TryGetProperty("content", out var messageContent))
            {
                return ReadContentText(messageContent);
            }
        }
        catch (JsonException)
        {
            // Algunos servidores envían eventos auxiliares; se ignoran.
        }

        return string.Empty;
    }

    private static string ReadContentText(JsonElement content)
    {
        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString() ?? string.Empty;
        }

        if (content.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        foreach (var item in content.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var text = item.GetString();
                if (!string.IsNullOrEmpty(text))
                {
                    parts.Add(text);
                }
                continue;
            }

            if (item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty("text", out var textProperty) &&
                textProperty.ValueKind == JsonValueKind.String)
            {
                var text = textProperty.GetString();
                if (!string.IsNullOrEmpty(text))
                {
                    parts.Add(text);
                }
            }
        }

        return string.Concat(parts);
    }

    private static string ParseAssistantText(string body)
    {
        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var choice = choices[0];
        if (!choice.TryGetProperty("message", out var message) ||
            !message.TryGetProperty("content", out var content))
        {
            return string.Empty;
        }

        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString()?.Trim() ?? string.Empty;
        }

        if (content.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        foreach (var item in content.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var text = item.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    parts.Add(text);
                }
                continue;
            }

            if (item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty("text", out var textProperty) &&
                textProperty.ValueKind == JsonValueKind.String)
            {
                var text = textProperty.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    parts.Add(text);
                }
            }
        }

        return string.Join(Environment.NewLine, parts).Trim();
    }

    private static string BuildHttpError(HttpStatusCode statusCode, string body)
    {
        var providerMessage = TryReadErrorMessage(body);
        var status = $"{(int)statusCode} {statusCode}";
        return string.IsNullOrWhiteSpace(providerMessage)
            ? $"El proveedor rechazó la solicitud ({status})."
            : $"El proveedor rechazó la solicitud ({status}): {providerMessage}";
    }

    private static string? TryReadErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                {
                    return error.GetString();
                }

                if (error.ValueKind == JsonValueKind.Object &&
                    error.TryGetProperty("message", out var message) &&
                    message.ValueKind == JsonValueKind.String)
                {
                    return message.GetString();
                }
            }

            if (document.RootElement.TryGetProperty("detail", out var detail) &&
                detail.ValueKind == JsonValueKind.String)
            {
                return detail.GetString();
            }
        }
        catch (JsonException)
        {
            // El cuerpo se resumirá como texto plano.
        }

        var compact = body.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return compact.Length <= 240 ? compact : compact[..240] + "…";
    }

    private sealed record ChatCompletionRequest(
        string Model,
        IReadOnlyList<ChatMessage> Messages,
        bool Stream);

    private sealed record ChatMessage(string Role, string Content);
}
