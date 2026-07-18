namespace Nexo.Core.Voice;

public interface IVoiceInputService : IDisposable
{
    bool IsListening { get; }

    Task<VoiceStartResult> StartListeningAsync(CancellationToken cancellationToken = default);

    Task<VoiceRecognitionResult> StopListeningAsync(CancellationToken cancellationToken = default);

    Task CancelAsync();
}
