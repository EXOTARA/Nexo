namespace Nexo.Core.Voice;

public sealed class WakeWordRecognitionObservedEventArgs : EventArgs
{
    public WakeWordRecognitionObservedEventArgs(
        WakeWordPhrase phrase,
        string recognizedText,
        bool isFinal,
        WakeWordMatchResult match)
    {
        Phrase = phrase;
        RecognizedText = recognizedText ?? string.Empty;
        IsFinal = isFinal;
        Match = match ?? throw new ArgumentNullException(nameof(match));
    }

    public WakeWordPhrase Phrase { get; }

    public string RecognizedText { get; }

    public bool IsFinal { get; }

    public WakeWordMatchResult Match { get; }
}
