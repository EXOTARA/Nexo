namespace Nexo.Core.Voice;

public interface IWakeWordService : IDisposable
{
    event EventHandler<WakeWordDetectedEventArgs>? WakeWordDetected;

    bool IsReady { get; }

    bool IsListening { get; }

    int InputDeviceNumber { get; set; }

    Task<VoicePreparationResult> PrepareAsync(
        IProgress<VoicePreparationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<VoiceStartResult> StartListeningAsync(
        WakeWordPhrase phrase,
        CancellationToken cancellationToken = default);

    Task StopListeningAsync();
}
