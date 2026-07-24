using Nexo.Core.Voice;

namespace Nexo.Windows.Voice;

/// <summary>
/// Ámbito de exclusión sobre las operaciones mutantes de <see cref="IWakeWordService"/>.
/// Se obtiene con <see cref="VoiceCoordinator.AcquireWakeWordScopeAsync"/>, que adquiere el
/// único semáforo de wake word del coordinador; el ámbito lo libera —exactamente una vez— al
/// hacer <see cref="IAsyncDisposable.DisposeAsync"/>.
///
/// Mismo contrato que <see cref="IVoiceInputScope"/>: las operaciones de wake word solo son
/// alcanzables a través de este ámbito, de modo que el semáforo permanece privado en
/// <see cref="VoiceCoordinator"/>. El orden de adquisición es siempre entrada de voz primero y
/// wake word después; nunca al revés.
/// </summary>
public interface IWakeWordScope : IAsyncDisposable
{
    Task<VoiceStartResult> StartListeningAsync(
        WakeWordPhrase phrase,
        CancellationToken cancellationToken = default);

    Task StopListeningAsync();
}
