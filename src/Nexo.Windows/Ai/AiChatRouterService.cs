using System.Runtime.CompilerServices;
using Nexo.Core.Ai;

namespace Nexo.Windows.Ai;

public sealed class AiChatRouterService : IAiChatService, IDisposable
{
    private readonly OpenAiCompatibleChatService _compatibleService;
    private readonly OllamaNativeChatService _ollamaService;

    public AiChatRouterService(HttpClient? compatibleClient = null, HttpClient? ollamaClient = null)
    {
        _compatibleService = new OpenAiCompatibleChatService(compatibleClient);
        _ollamaService = new OllamaNativeChatService(ollamaClient);
    }

    public Task<AiConnectionResult> TestConnectionAsync(
        AiProviderConfiguration configuration,
        CancellationToken cancellationToken = default) =>
        Resolve(configuration).TestConnectionAsync(configuration, cancellationToken);

    public Task<AiChatResult> SendAsync(
        AiProviderConfiguration configuration,
        AiChatRequest request,
        CancellationToken cancellationToken = default) =>
        Resolve(configuration).SendAsync(configuration, request, cancellationToken);

    public async IAsyncEnumerable<string> StreamAsync(
        AiProviderConfiguration configuration,
        AiChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var chunk in Resolve(configuration).StreamAsync(
                           configuration,
                           request,
                           cancellationToken))
        {
            yield return chunk;
        }
    }

    public void Dispose()
    {
        _compatibleService.Dispose();
        _ollamaService.Dispose();
    }

    private IAiChatService Resolve(AiProviderConfiguration configuration) =>
        configuration.Provider == AiProviderKind.Ollama
            ? _ollamaService
            : _compatibleService;
}
