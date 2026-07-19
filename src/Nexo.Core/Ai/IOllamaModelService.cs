namespace Nexo.Core.Ai;

public interface IOllamaModelService
{
    Task<IReadOnlyList<OllamaModelInfo>> ListAsync(
        string baseUrl,
        CancellationToken cancellationToken = default);

    Task<OllamaOperationResult> PullAsync(
        string baseUrl,
        string model,
        IProgress<OllamaPullProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<OllamaOperationResult> DeleteAsync(
        string baseUrl,
        string model,
        CancellationToken cancellationToken = default);
}
