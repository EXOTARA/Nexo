namespace Nexo.Core.Voice;

public interface IVoiceOutputService : IDisposable
{
    void SpeakShort(string text);

    void Stop();
}
