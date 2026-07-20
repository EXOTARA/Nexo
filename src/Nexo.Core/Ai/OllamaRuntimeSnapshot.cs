namespace Nexo.Core.Ai;

public sealed record OllamaRuntimeSnapshot(
    OllamaRuntimeState State,
    string BaseUrl,
    string? ExecutablePath,
    string Message)
{
    public bool IsRunning =>
        State is OllamaRuntimeState.ExternalRunning
            or OllamaRuntimeState.ManagedRunning;

    public bool IsManaged =>
        State is OllamaRuntimeState.ManagedInstalled
            or OllamaRuntimeState.ManagedRunning;

    public bool CanStartManaged =>
        State == OllamaRuntimeState.ManagedInstalled;
}