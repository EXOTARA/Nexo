using System.Net;
using Nexo.Core.Ai;
using Nexo.Core.Assistant;
using Nexo.Windows.Ai;
using Xunit;

namespace Nexo.Windows.Tests.Ai;

/// <summary>
/// Diseño D30 — encontrado en uso real: alguien adjuntó una captura de pantalla y le preguntó algo
/// a Groq con <c>llama-3.3-70b-versatile</c>, un modelo de solo texto. Groq lo rechazó con
/// «messages[5].content must be a string», y eso es lo único que veía la persona en el chat — nada
/// que sugiriera que el problema era la imagen. Estas pruebas fijan que, cuando la petición llevaba
/// una imagen y el proveedor responde 400, Sakura añade una explicación en español; y que en
/// cualquier otro caso —sin imagen, o un código de error distinto— no inventa una causa que no
/// tiene forma de confirmar.
/// </summary>
public sealed class OpenAiCompatibleChatServiceTests
{
    private static readonly AiProviderConfiguration Configuration = new(
        AiProviderKind.OpenAICompatible,
        "https://api.groq.com/openai/v1",
        "llama-3.3-70b-versatile",
        string.Empty)
    {
        StoredApiKey = "test-key"
    };

    private const string GroqRejectionBody =
        """{"error":{"message":"messages[5].content must be a string"}}""";

    [Fact]
    public async Task WhenAnImageWasSentAndTheProviderReturnsBadRequestItExplainsTheLikelyCause()
    {
        using var handler = new FakeHandler(HttpStatusCode.BadRequest, GroqRejectionBody);
        using var service = new OpenAiCompatibleChatService(new HttpClient(handler));

        var result = await service.SendAsync(Configuration, RequestWithImage());

        Assert.False(result.IsSuccess);
        Assert.Contains("messages[5].content must be a string", result.Detail);
        Assert.Contains("no admita imágenes", result.Detail);
    }

    [Fact]
    public async Task WhenNoImageWasSentTheBadRequestExplanationIsNotAdded()
    {
        using var handler = new FakeHandler(HttpStatusCode.BadRequest, GroqRejectionBody);
        using var service = new OpenAiCompatibleChatService(new HttpClient(handler));

        var result = await service.SendAsync(Configuration, RequestWithoutImage());

        Assert.False(result.IsSuccess);
        Assert.Contains("messages[5].content must be a string", result.Detail);
        Assert.DoesNotContain("imágenes", result.Detail);
    }

    [Fact]
    public async Task AnImageAttachedWithAnUnrelatedServerErrorDoesNotBlameImages()
    {
        // Un 500 con una imagen adjunta no es evidencia de que la imagen fue la causa — podría ser
        // cualquier cosa del lado del proveedor. Solo el 400 es la señal que se usa.
        using var handler = new FakeHandler(HttpStatusCode.InternalServerError, "{}");
        using var service = new OpenAiCompatibleChatService(new HttpClient(handler));

        var result = await service.SendAsync(Configuration, RequestWithImage());

        Assert.False(result.IsSuccess);
        Assert.DoesNotContain("imágenes", result.Detail);
    }

    /// <summary>
    /// El cuerpo textual que devuelve Gemini, copiado de una respuesta real. La raíz es un array, y
    /// eso es lo que tiraba la aplicación entera.
    /// </summary>
    private const string GeminiArrayWrappedError = """
    [{
      "error": {
        "code": 400,
        "message": "Please pass a valid API key",
        "status": "INVALID_ARGUMENT"
      }
    }
    ]
    """;

    [Fact]
    public async Task AnErrorWrappedInAnArrayDoesNotBringDownTheApplication()
    {
        // Regresión de una caída real: Sakura se cerraba entera al primer error con Gemini.
        // TryGetProperty sobre un array no devuelve falso, lanza, y esa excepción no estaba
        // capturada. Que este método devuelva en vez de lanzar es la prueba.
        using var handler = new FakeHandler(HttpStatusCode.BadRequest, GeminiArrayWrappedError);
        using var service = new OpenAiCompatibleChatService(new HttpClient(handler));

        var result = await service.SendAsync(Configuration, RequestWithoutImage());

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task TheMessageInsideTheArrayIsWhatGetsShown()
    {
        // Y no solo no caerse: sacar el mensaje útil. Volcar el JSON crudo dejaría a la persona
        // leyendo llaves para enterarse de que su clave no vale.
        using var handler = new FakeHandler(HttpStatusCode.BadRequest, GeminiArrayWrappedError);
        using var service = new OpenAiCompatibleChatService(new HttpClient(handler));

        var result = await service.SendAsync(Configuration, RequestWithoutImage());

        Assert.Contains("Please pass a valid API key", result.Detail);
        Assert.DoesNotContain("INVALID_ARGUMENT", result.Detail);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[[[[[\"demasiado hondo\"]]]]]")]
    [InlineData("\"solo una cadena\"")]
    [InlineData("12345")]
    [InlineData("null")]
    [InlineData("{\"error\": [\"vaya\"]}")]
    [InlineData("{\"error\": {\"message\": 42}}")]
    public async Task AnyOtherShapeOfBodyIsSurvivable(string body)
    {
        // El cuerpo lo escribe el proveedor, no nosotros. Ninguna forma —por rara que sea— puede
        // volver a cerrar la aplicación desde el código que sirve para explicar un error.
        using var handler = new FakeHandler(HttpStatusCode.BadRequest, body);
        using var service = new OpenAiCompatibleChatService(new HttpClient(handler));

        var result = await service.SendAsync(Configuration, RequestWithoutImage());

        Assert.False(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Detail));
    }

    private static AiChatRequest RequestWithImage() => new(
        [new ConversationMessage(ConversationRole.User, "¿Qué ves aquí?", DateTimeOffset.Now)],
        Instructions: "Eres Sakura.",
        Images: [AiImageAttachment.FromBytes([1, 2, 3, 4])]);

    private static AiChatRequest RequestWithoutImage() => new(
        [new ConversationMessage(ConversationRole.User, "Hola", DateTimeOffset.Now)],
        Instructions: "Eres Sakura.");

    private sealed class FakeHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body)
            });
    }
}
