namespace Nexo.Core.Voice;

public sealed class WakeWordDetectedEventArgs : EventArgs
{
    private readonly byte[] _preRollAudio;

    public WakeWordDetectedEventArgs(
        WakeWordPhrase phrase,
        string recognizedText,
        ReadOnlyMemory<byte> preRollAudio = default)
    {
        Phrase = phrase;
        RecognizedText = recognizedText;
        _preRollAudio = preRollAudio.IsEmpty
            ? []
            : preRollAudio.ToArray();
    }

    public WakeWordPhrase Phrase { get; }

    public string RecognizedText { get; }

    /// <summary>
    /// PCM mono de 16 kHz y 16 bits capturado justo antes del traspaso a Whisper.
    /// Se usa para no perder el inicio de órdenes como “Nexo, abre PowerShell”.
    /// </summary>
    public ReadOnlyMemory<byte> PreRollAudio => _preRollAudio;
}
