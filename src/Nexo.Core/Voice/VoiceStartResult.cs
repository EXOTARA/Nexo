namespace Nexo.Core.Voice;

public sealed record VoiceStartResult(bool IsAvailable, string Detail)
{
    public static VoiceStartResult Started(string recognizerName) =>
        new(true, string.IsNullOrWhiteSpace(recognizerName)
            ? "Micrófono listo."
            : $"Micrófono listo · {recognizerName}");

    public static VoiceStartResult Unavailable(string detail) =>
        new(false, detail);
}
