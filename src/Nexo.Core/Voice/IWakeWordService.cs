namespace Nexo.Core.Voice;

public interface IWakeWordService : IDisposable
{
    event EventHandler<WakeWordDetectedEventArgs>? WakeWordDetected;

    event EventHandler<WakeWordRecognitionObservedEventArgs>? RecognitionObserved;

    bool IsReady { get; }

    bool IsListening { get; }

    int InputDeviceNumber { get; set; }

    WakeWordSensitivity Sensitivity { get; set; }

    IReadOnlyList<string> CustomAliases { get; set; }

    Task<VoicePreparationResult> PrepareAsync(
        IProgress<VoicePreparationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<VoiceStartResult> StartListeningAsync(
        WakeWordPhrase phrase,
        CancellationToken cancellationToken = default);

    Task StopListeningAsync();
}
