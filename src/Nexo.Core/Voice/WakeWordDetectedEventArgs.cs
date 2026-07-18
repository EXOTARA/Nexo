namespace Nexo.Core.Voice;

public sealed class WakeWordDetectedEventArgs : EventArgs
{
    public WakeWordDetectedEventArgs(WakeWordPhrase phrase, string recognizedText)
    {
        Phrase = phrase;
        RecognizedText = recognizedText;
    }

    public WakeWordPhrase Phrase { get; }

    public string RecognizedText { get; }
}
