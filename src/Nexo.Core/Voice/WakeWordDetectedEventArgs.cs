namespace Nexo.Core.Voice;

public sealed class WakeWordDetectedEventArgs : EventArgs
{
    private readonly byte[] _preRollAudio;
    private readonly byte[] _postWakeAudio;

    public WakeWordDetectedEventArgs(
        WakeWordPhrase phrase,
        string recognizedText,
        ReadOnlyMemory<byte> preRollAudio = default,
        ReadOnlyMemory<byte> postWakeAudio = default,
        WakeWordMatchKind matchKind = WakeWordMatchKind.Exact)
    {
        Phrase = phrase;
        RecognizedText = recognizedText;
        MatchKind = matchKind;
        _preRollAudio = preRollAudio.IsEmpty
            ? []
            : preRollAudio.ToArray();
        _postWakeAudio = postWakeAudio.IsEmpty
            ? []
            : postWakeAudio.ToArray();
    }

    public WakeWordPhrase Phrase { get; }

    public string RecognizedText { get; }

    public WakeWordMatchKind MatchKind { get; }

    /// <summary>
    /// PCM mono de 16 kHz y 16 bits capturado justo antes del traspaso a Whisper.
    /// Se usa para no perder el inicio de órdenes como “Kohana, abre PowerShell”.
    /// </summary>
    public ReadOnlyMemory<byte> PreRollAudio => _preRollAudio;

    /// <summary>
    /// PCM capturado después de detectar la frase y antes de entregar el
    /// micrófono a Whisper. Sirve como evidencia de que la orden comenzó
    /// inmediatamente después de “Hey Kohana”.
    /// </summary>
    public ReadOnlyMemory<byte> PostWakeAudio => _postWakeAudio;
}
