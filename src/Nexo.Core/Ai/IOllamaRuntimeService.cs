namespace Nexo.Core.Ai;

public interface IOllamaRuntimeService
{
    Task<OllamaRuntimeSnapshot> InspectAsync(
        CancellationToken cancellationToken = default);
}