using Nexo.Core.Voice;

namespace Nexo.Windows.Voice;

/// <summary>
/// Ámbito de exclusión sobre las operaciones mutantes de <see cref="IVoiceInputService"/>.
/// Se obtiene con <see cref="VoiceCoordinator.AcquireVoiceInputScopeAsync"/>, que adquiere el
/// único semáforo de entrada de voz del coordinador; el ámbito lo libera —exactamente una
/// vez— al hacer <see cref="IAsyncDisposable.DisposeAsync"/>.
///
/// Las operaciones de entrada de voz solo son alcanzables a través de este ámbito: así el
/// semáforo permanece privado en <see cref="VoiceCoordinator"/> y es imposible mutar el
/// servicio sin sostener la exclusión. El orquestador (la vista principal) mantiene el ámbito
/// mientras dura su sección crítica y hace toda su orquestación visual dentro de él.
/// </summary>
public interface IVoiceInputScope : IAsyncDisposable
{
    Task<VoiceStartResult> StartListeningAsync(CancellationToken cancellationToken = default);

    Task<VoiceRecognitionResult> StopListeningAsync(CancellationToken cancellationToken = default);

    Task CancelAsync();

    Task<VoiceRecognitionResult> ListenForUtteranceAsync(
        TimeSpan maximumDuration,
        TimeSpan trailingSilence,
        ReadOnlyMemory<byte> initialPcmAudio = default,
        ReadOnlyMemory<byte> initialSpeechPcmAudio = default,
        CancellationToken cancellationToken = default);
}
