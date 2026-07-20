namespace Nexo.Core.Ai;

public interface IOllamaRuntimeService
{
    Task<OllamaRuntimeSnapshot> InspectAsync(
        CancellationToken cancellationToken = default);

    Task<OllamaRuntimeSnapshot> StartManagedAsync(
        CancellationToken cancellationToken = default);
}
