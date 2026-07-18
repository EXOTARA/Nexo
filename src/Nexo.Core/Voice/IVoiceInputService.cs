namespace Nexo.Core.Voice;

public interface IVoiceInputService : IDisposable
{
    bool IsReady { get; }

    bool IsListening { get; }

    Task<VoicePreparationResult> PrepareAsync(
        IProgress<VoicePreparationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<VoiceStartResult> StartListeningAsync(CancellationToken cancellationToken = default);

    Task<VoiceRecognitionResult> StopListeningAsync(CancellationToken cancellationToken = default);

    Task<VoiceRecognitionResult> ListenForUtteranceAsync(
        TimeSpan maximumDuration,
        TimeSpan trailingSilence,
        CancellationToken cancellationToken = default);

    Task CancelAsync();
}
