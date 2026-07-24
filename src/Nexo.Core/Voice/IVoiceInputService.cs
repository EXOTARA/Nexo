namespace Nexo.Core.Voice;

public interface IVoiceInputService : IDisposable
{
    bool IsReady { get; }

    bool IsListening { get; }

    int InputDeviceNumber { get; set; }

    IReadOnlyList<VoiceInputDevice> GetInputDevices();

    Task<VoicePreparationResult> PrepareAsync(
        IProgress<VoicePreparationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<VoiceStartResult> StartListeningAsync(CancellationToken cancellationToken = default);

    Task<VoiceRecognitionResult> StopListeningAsync(CancellationToken cancellationToken = default);

    Task<VoiceRecognitionResult> ListenForUtteranceAsync(
        TimeSpan maximumDuration,
        TimeSpan trailingSilence,
        ReadOnlyMemory<byte> initialPcmAudio = default,
        ReadOnlyMemory<byte> initialSpeechPcmAudio = default,
        CancellationToken cancellationToken = default);

    Task CancelAsync();
}
