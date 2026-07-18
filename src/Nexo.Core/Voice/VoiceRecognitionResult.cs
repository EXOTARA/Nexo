namespace Nexo.Core.Voice;

public sealed record VoiceRecognitionResult(
    bool IsRecognized,
    string Text,
    double Confidence,
    string Detail)
{
    public static VoiceRecognitionResult Recognized(string text, double confidence) =>
        new(true, text.Trim(), Math.Clamp(confidence, 0, 1), "Voz reconocida.");

    public static VoiceRecognitionResult NoSpeech(string detail) =>
        new(false, string.Empty, 0, detail);
}
