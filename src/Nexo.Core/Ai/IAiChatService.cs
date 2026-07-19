namespace Nexo.Core.Ai;

public interface IAiChatService
{
    Task<AiConnectionResult> TestConnectionAsync(
        AiProviderConfiguration configuration,
        CancellationToken cancellationToken = default);

    Task<AiChatResult> SendAsync(
        AiProviderConfiguration configuration,
        AiChatRequest request,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamAsync(
        AiProviderConfiguration configuration,
        AiChatRequest request,
        CancellationToken cancellationToken = default);
}
