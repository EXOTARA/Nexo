using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nexo.Core.Ai;
using Nexo.Core.Assistant;

namespace Nexo.Windows.Ai;

public sealed class OllamaNativeChatService : IAiChatService, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonElement DiagnosticSchema = CreateDiagnosticSchema();

    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    public OllamaNativeChatService(HttpClient? httpClient = null)
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
                BuildOllamaEndpoint(configuration.BaseUrl, "tags"));
            ApplyAuthentication(request, configuration);

            using var timeout = CreateTimeout(cancellationToken, TimeSpan.FromSeconds(20));
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                return AiConnectionResult.Failed(BuildHttpError(response.StatusCode, body));
            }

            var models = ParseModels(body);
            var detail = models.Count == 0
                ? "Ollama respondió, pero no informó modelos disponibles."
                : $"Ollama conectado · {models.Count} modelo(s) disponible(s).";

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
                "La conexión tardó demasiado. Comprueba que Ollama esté iniciado.");
        }
        catch (HttpRequestException exception)
        {
            return AiConnectionResult.Failed(
                $"No pude conectar con Ollama: {exception.Message}");
        }
        catch (JsonException)
        {
            return AiConnectionResult.Failed(
                "Ollama respondió, pero la lista de modelos no tenía el formato esperado.");
        }
    }

    public async Task<AiChatResult> SendAsync(
        AiProviderConfiguration configuration,
        AiChatRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = ValidateRequest(configuration, request);
        if (validation is not null)
        {
            return AiChatResult.Failed(validation);
        }

        try
        {
            if (request.Mode == AiRequestMode.VisionTechnicalDiagnostic)
            {
                var diagnostic = await GenerateVisionDiagnosticAsync(
                    configuration,
                    request,
                    cancellationToken);
                return AiChatResult.Success(diagnostic);
            }

            var result = await SendChatAsync(
                configuration,
                request,
                request.Instructions,
                temperature: request.Mode == AiRequestMode.VisionGeneral ? 0.2 : 0.35,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(result.Content) &&
                !string.IsNullOrWhiteSpace(result.Thinking))
            {
                result = await SendChatAsync(
                    configuration,
                    request,
                    BuildDirectAnswerInstructions(request.Instructions),
                    temperature: 0.2,
                    cancellationToken);
            }

            return string.IsNullOrWhiteSpace(result.Content)
                ? AiChatResult.Failed("Ollama respondió sin texto final utilizable.")
                : AiChatResult.Success(result.Content);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return AiChatResult.Failed(
                "La respuesta tardó demasiado. Puedes intentar de nuevo o usar un modelo más ligero.");
        }
        catch (HttpRequestException exception)
        {
            return AiChatResult.Failed($"No pude conectar con Ollama: {exception.Message}");
        }
        catch (JsonException)
        {
            return AiChatResult.Failed(
                "Ollama respondió con un formato que Nexo todavía no reconoce.");
        }
    }

    public async IAsyncEnumerable<string> StreamAsync(
        AiProviderConfiguration configuration,
        AiChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = ValidateRequest(configuration, request);
        if (validation is not null)
        {
            throw new AiChatStreamException(validation);
        }

        if (request.Mode == AiRequestMode.VisionTechnicalDiagnostic)
        {
            string diagnostic;
            try
            {
                diagnostic = await GenerateVisionDiagnosticAsync(
                    configuration,
                    request,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new AiChatStreamException(
                    "El diagnóstico visual tardó demasiado. Intenta con un recorte más pequeño.");
            }
            catch (HttpRequestException exception)
            {
                throw new AiChatStreamException(
                    $"No pude conectar con Ollama: {exception.Message}",
                    exception);
            }
            catch (JsonException exception)
            {
                throw new AiChatStreamException(
                    "Ollama no devolvió un diagnóstico visual válido.",
                    exception);
            }

            yield return diagnostic;
            yield break;
        }

        var payload = new OllamaChatRequest(
            configuration.Model.Trim(),
            BuildMessages(request, request.Instructions),
            Stream: true,
            Think: false,
            KeepAlive: "10m",
            Format: null,
            Options: new OllamaOptions(4096, request.Mode == AiRequestMode.VisionGeneral ? 0.2 : 0.35));

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            BuildOllamaEndpoint(configuration.BaseUrl, "chat"))
        {
            Content = JsonContent.Create(payload, options: SerializerOptions)
        };
        ApplyAuthentication(httpRequest, configuration);

        using var timeout = CreateTimeout(cancellationToken, TimeSpan.FromSeconds(120));
        using var response = await SendStreamingRequestAsync(
            httpRequest,
            cancellationToken,
            timeout.Token);

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var reader = new StreamReader(stream);
        var emittedText = false;
        var receivedThinking = false;

        while (true)
        {
            var line = await reader.ReadLineAsync(timeout.Token);
            if (line is null)
            {
                break;
            }

            var chunk = ParseStreamChunk(line);
            if (!string.IsNullOrWhiteSpace(chunk.Error))
            {
                throw new AiChatStreamException(chunk.Error);
            }

            receivedThinking |= !string.IsNullOrEmpty(chunk.Thinking);
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                emittedText = true;
                yield return chunk.Content;
            }

            if (chunk.Done)
            {
                break;
            }
        }

        if (emittedText)
        {
            yield break;
        }

        if (receivedThinking)
        {
            var fallback = await SendChatAsync(
                configuration,
                request,
                BuildDirectAnswerInstructions(request.Instructions),
                temperature: 0.2,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(fallback.Content))
            {
                yield return fallback.Content;
                yield break;
            }
        }

        throw new AiChatStreamException(
            "Ollama terminó la respuesta sin enviar texto final utilizable.");
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }

    private async Task<string> GenerateVisionDiagnosticAsync(
        AiProviderConfiguration configuration,
        AiChatRequest request,
        CancellationToken cancellationToken)
    {
        var evidence = await ExtractVisionEvidenceAsync(
            configuration,
            request,
            cancellationToken);

        var responseInstructions =
            request.Instructions + "\n\n" +
            VisionDiagnosticPromptBuilder.BuildResponseInstructions(evidence);

        var answer = await SendChatAsync(
            configuration,
            request,
            responseInstructions,
            temperature: 0.1,
            cancellationToken);

        var finalText = answer.Content;
        if (string.IsNullOrWhiteSpace(finalText) ||
            VisionResponseQuality.IsTooGeneric(finalText, evidence))
        {
            var retryInstructions =
                responseInstructions + "\n\n" +
                "La respuesta anterior fue demasiado genérica. Reescribe el diagnóstico con una causa concreta, " +
                "un cambio exacto y una comprobación verificable. No menciones soporte técnico.";

            answer = await SendChatAsync(
                configuration,
                request,
                retryInstructions,
                temperature: 0,
                cancellationToken);
            finalText = answer.Content;
        }

        if (string.IsNullOrWhiteSpace(finalText))
        {
            finalText = evidence.ErrorVisible
                ? "Pude detectar un problema en la captura, pero el modelo no produjo una explicación final. Intenta recortar únicamente el error y vuelve a preguntar."
                : "No encontré un error legible en la captura. Selecciona una zona donde aparezcan completos el mensaje, el archivo y la línea.";
        }

        return VisionDiagnosticPromptBuilder.BuildEvidenceBanner(evidence) +
               Environment.NewLine + Environment.NewLine +
               finalText.Trim();
    }

    private async Task<VisionDiagnosticEvidence> ExtractVisionEvidenceAsync(
        AiProviderConfiguration configuration,
        AiChatRequest request,
        CancellationToken cancellationToken)
    {
        var lastQuestion = request.Messages
            .LastOrDefault(message => message.Role == ConversationRole.User)
            ?.Text;

        var extractionPrompt =
            VisionDiagnosticPromptBuilder.BuildExtractionPrompt(lastQuestion);
        if (!string.IsNullOrWhiteSpace(request.SystemContext))
        {
            extractionPrompt +=
                "\n\nTexto auxiliar extraído localmente o contexto autorizado. " +
                "Compruébalo contra la imagen y no lo trates como infalible:\n" +
                request.SystemContext.Trim();
        }

        var extractionMessages = new List<OllamaMessage>
        {
            new("system", VisionDiagnosticPromptBuilder.ExtractionInstructions),
            new(
                "user",
                extractionPrompt,
                request.Images?.Select(image => image.Base64Data).ToArray())
        };

        var payload = new OllamaChatRequest(
            configuration.Model.Trim(),
            extractionMessages,
            Stream: false,
            Think: false,
            KeepAlive: "10m",
            Format: DiagnosticSchema,
            Options: new OllamaOptions(4096, 0));

        var response = await SendPayloadAsync(
            configuration,
            payload,
            cancellationToken,
            TimeSpan.FromSeconds(120));

        if (string.IsNullOrWhiteSpace(response.Content))
        {
            return new VisionDiagnosticEvidence
            {
                ErrorVisible = false,
                MissingInformation = "Ollama no devolvió evidencia visual estructurada.",
                Confidence = 0
            };
        }

        var json = StripJsonFence(response.Content);
        var evidence = JsonSerializer.Deserialize<VisionDiagnosticEvidence>(
            json,
            SerializerOptions);

        return evidence?.Normalize() ?? new VisionDiagnosticEvidence
        {
            ErrorVisible = false,
            MissingInformation = "No pude interpretar la evidencia visual devuelta por el modelo.",
            Confidence = 0
        };
    }

    private async Task<OllamaResponseData> SendChatAsync(
        AiProviderConfiguration configuration,
        AiChatRequest request,
        string instructions,
        double temperature,
        CancellationToken cancellationToken)
    {
        var payload = new OllamaChatRequest(
            configuration.Model.Trim(),
            BuildMessages(request, instructions),
            Stream: false,
            Think: false,
            KeepAlive: "10m",
            Format: null,
            Options: new OllamaOptions(4096, temperature));

        return await SendPayloadAsync(
            configuration,
            payload,
            cancellationToken,
            TimeSpan.FromSeconds(120));
    }

    private async Task<OllamaResponseData> SendPayloadAsync(
        AiProviderConfiguration configuration,
        OllamaChatRequest payload,
        CancellationToken cancellationToken,
        TimeSpan duration)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildOllamaEndpoint(configuration.BaseUrl, "chat"))
        {
            Content = JsonContent.Create(payload, options: SerializerOptions)
        };
        ApplyAuthentication(request, configuration);

        using var timeout = CreateTimeout(cancellationToken, duration);
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeout.Token);
        var body = await response.Content.ReadAsStringAsync(timeout.Token);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(BuildHttpError(response.StatusCode, body));
        }

        return ParseResponse(body);
    }

    private async Task<HttpResponseMessage> SendStreamingRequestAsync(
        HttpRequestMessage request,
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
                $"No pude conectar con Ollama: {exception.Message}",
                exception);
        }
    }

    private static List<OllamaMessage> BuildMessages(
        AiChatRequest request,
        string instructions)
    {
        var systemText = instructions.Trim();
        if (!string.IsNullOrWhiteSpace(request.SystemContext))
        {
            systemText += $"\n\nContexto autorizado por el usuario:\n{request.SystemContext.Trim()}";
        }

        var messages = new List<OllamaMessage>
        {
            new("system", systemText)
        };

        var conversation = request.Messages
            .Where(item => !string.IsNullOrWhiteSpace(item.Text))
            .TakeLast(20)
            .ToArray();

        for (var index = 0; index < conversation.Length; index++)
        {
            var message = conversation[index];
            var role = message.Role == ConversationRole.User ? "user" : "assistant";
            var isLastUserMessage =
                index == conversation.Length - 1 &&
                message.Role == ConversationRole.User &&
                request.Images is { Count: > 0 };

            messages.Add(new OllamaMessage(
                role,
                message.Text.Trim(),
                isLastUserMessage
                    ? request.Images!.Select(image => image.Base64Data).ToArray()
                    : null));
        }

        return messages;
    }

    private static string BuildDirectAnswerInstructions(string instructions)
    {
        return instructions.Trim() + "\n\n" +
               "/no_think\n" +
               "Responde directamente en el campo final de contenido. No dejes la respuesta final vacía y no muestres razonamiento interno.";
    }

    private static string? ValidateRequest(
        AiProviderConfiguration configuration,
        AiChatRequest request)
    {
        var validation = ValidateConfiguration(configuration);
        if (validation is not null)
        {
            return validation;
        }

        if (string.IsNullOrWhiteSpace(configuration.Model))
        {
            return "Selecciona o escribe un modelo en Personalización → Inteligencia artificial.";
        }

        if (!request.Messages.Any(message =>
                message.Role == ConversationRole.User &&
                !string.IsNullOrWhiteSpace(message.Text)))
        {
            return "No había una consulta para enviar a Ollama.";
        }

        if ((request.Mode is AiRequestMode.VisionGeneral or AiRequestMode.VisionTechnicalDiagnostic) &&
            request.Images is not { Count: > 0 })
        {
            return "La consulta visual no incluía una captura.";
        }

        return null;
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
            return "La URL de Ollama no es válida.";
        }

        return null;
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

    private static Uri BuildOllamaEndpoint(string baseUrl, string relativePath)
    {
        var normalized = AiProviderDefaults.NormalizeBaseUrl(baseUrl);
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

    private static IReadOnlyList<string> ParseModels(string body)
    {
        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("models", out var models) ||
            models.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return models.EnumerateArray()
            .Select(item =>
            {
                if (item.TryGetProperty("name", out var name) &&
                    name.ValueKind == JsonValueKind.String)
                {
                    return name.GetString();
                }

                if (item.TryGetProperty("model", out var model) &&
                    model.ValueKind == JsonValueKind.String)
                {
                    return model.GetString();
                }

                return null;
            })
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static OllamaResponseData ParseResponse(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        if (root.TryGetProperty("error", out var error) &&
            error.ValueKind == JsonValueKind.String)
        {
            throw new HttpRequestException(error.GetString());
        }

        if (!root.TryGetProperty("message", out var message) ||
            message.ValueKind != JsonValueKind.Object)
        {
            return new OllamaResponseData(string.Empty, string.Empty);
        }

        var content = message.TryGetProperty("content", out var contentProperty) &&
                      contentProperty.ValueKind == JsonValueKind.String
            ? contentProperty.GetString() ?? string.Empty
            : string.Empty;

        var thinking = message.TryGetProperty("thinking", out var thinkingProperty) &&
                       thinkingProperty.ValueKind == JsonValueKind.String
            ? thinkingProperty.GetString() ?? string.Empty
            : string.Empty;

        return new OllamaResponseData(content.Trim(), thinking.Trim());
    }

    private static OllamaStreamChunk ParseStreamChunk(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return new OllamaStreamChunk(string.Empty, string.Empty, false, string.Empty);
        }

        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;

        var error = root.TryGetProperty("error", out var errorProperty) &&
                    errorProperty.ValueKind == JsonValueKind.String
            ? errorProperty.GetString() ?? string.Empty
            : string.Empty;

        var done = root.TryGetProperty("done", out var doneProperty) &&
                   doneProperty.ValueKind == JsonValueKind.True;

        if (!root.TryGetProperty("message", out var message) ||
            message.ValueKind != JsonValueKind.Object)
        {
            return new OllamaStreamChunk(string.Empty, string.Empty, done, error);
        }

        var content = message.TryGetProperty("content", out var contentProperty) &&
                      contentProperty.ValueKind == JsonValueKind.String
            ? contentProperty.GetString() ?? string.Empty
            : string.Empty;

        var thinking = message.TryGetProperty("thinking", out var thinkingProperty) &&
                       thinkingProperty.ValueKind == JsonValueKind.String
            ? thinkingProperty.GetString() ?? string.Empty
            : string.Empty;

        return new OllamaStreamChunk(content, thinking, done, error);
    }

    private static string StripJsonFence(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstNewLine = trimmed.IndexOf('\n');
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (firstNewLine < 0 || lastFence <= firstNewLine)
        {
            return trimmed;
        }

        return trimmed[(firstNewLine + 1)..lastFence].Trim();
    }

    private static string BuildHttpError(HttpStatusCode statusCode, string body)
    {
        var providerMessage = TryReadErrorMessage(body);
        var status = $"{(int)statusCode} {statusCode}";
        return string.IsNullOrWhiteSpace(providerMessage)
            ? $"Ollama rechazó la solicitud ({status})."
            : $"Ollama rechazó la solicitud ({status}): {providerMessage}";
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
        }
        catch (JsonException)
        {
            // Se resumirá como texto plano.
        }

        var compact = body.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return compact.Length <= 240 ? compact : compact[..240] + "…";
    }

    private static JsonElement CreateDiagnosticSchema()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "type": "object",
              "properties": {
                "errorVisible": { "type": "boolean" },
                "problemType": { "type": "string" },
                "errorCode": { "type": "string" },
                "fileName": { "type": "string" },
                "lineNumber": { "type": ["integer", "null"] },
                "visibleMessage": { "type": "string" },
                "visibleCommand": { "type": "string" },
                "relevantCode": { "type": "string" },
                "missingInformation": { "type": "string" },
                "confidence": { "type": "number", "minimum": 0, "maximum": 1 }
              },
              "required": [
                "errorVisible",
                "problemType",
                "errorCode",
                "fileName",
                "lineNumber",
                "visibleMessage",
                "visibleCommand",
                "relevantCode",
                "missingInformation",
                "confidence"
              ],
              "additionalProperties": false
            }
            """);

        return document.RootElement.Clone();
    }

    private sealed record OllamaChatRequest(
        string Model,
        IReadOnlyList<OllamaMessage> Messages,
        bool Stream,
        bool Think,
        [property: JsonPropertyName("keep_alive")] string KeepAlive,
        object? Format,
        OllamaOptions Options);

    private sealed record OllamaMessage(
        string Role,
        string Content,
        IReadOnlyList<string>? Images = null);

    private sealed record OllamaOptions(
        [property: JsonPropertyName("num_ctx")] int NumContext,
        double Temperature);

    private sealed record OllamaResponseData(string Content, string Thinking);

    private sealed record OllamaStreamChunk(
        string Content,
        string Thinking,
        bool Done,
        string Error);
}
