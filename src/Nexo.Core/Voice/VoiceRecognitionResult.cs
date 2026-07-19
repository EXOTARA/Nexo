namespace Nexo.Core.Voice;

public sealed record VoiceRecognitionResult(
    bool IsRecognized,
    string Text,
    double Confidence,
    string Detail,
    bool RequiresConfirmation)
{
    public static VoiceRecognitionResult Recognized(
        string text,
        double confidence,
        bool requiresConfirmation = false,
        string detail = "Voz reconocida.") =>
        new(
            true,
            text.Trim(),
            Math.Clamp(confidence, 0, 1),
            detail,
            requiresConfirmation);

    public static VoiceRecognitionResult NoSpeech(string detail) =>
        new(false, string.Empty, 0, detail, false);
}
