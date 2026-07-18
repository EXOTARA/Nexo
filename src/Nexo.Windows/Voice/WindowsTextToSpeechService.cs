using System.Speech.Synthesis;
using Nexo.Core.Voice;

namespace Nexo.Windows.Voice;

public sealed class WindowsTextToSpeechService : IVoiceOutputService
{
    private const int MaximumSpokenCharacters = 180;
    private readonly object _sync = new();
    private readonly SpeechSynthesizer _synthesizer = new();
    private bool _disposed;

    public WindowsTextToSpeechService()
    {
        _synthesizer.SetOutputToDefaultAudioDevice();

        var spanishVoice = _synthesizer
            .GetInstalledVoices()
            .FirstOrDefault(voice =>
                voice.Enabled &&
                voice.VoiceInfo.Culture.TwoLetterISOLanguageName.Equals(
                    "es",
                    StringComparison.OrdinalIgnoreCase));

        if (spanishVoice is not null)
        {
            _synthesizer.SelectVoice(spanishVoice.VoiceInfo.Name);
        }
    }

    public void SpeakShort(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var spokenText = text.Trim();
        if (spokenText.Length > MaximumSpokenCharacters)
        {
            spokenText = spokenText[..MaximumSpokenCharacters].TrimEnd() + "…";
        }

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _synthesizer.SpeakAsyncCancelAll();
            _synthesizer.SpeakAsync(spokenText);
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            if (!_disposed)
            {
                _synthesizer.SpeakAsyncCancelAll();
            }
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _synthesizer.SpeakAsyncCancelAll();
            _synthesizer.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
