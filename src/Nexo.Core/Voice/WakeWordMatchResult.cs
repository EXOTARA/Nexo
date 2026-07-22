namespace Nexo.Core.Voice;

public sealed record WakeWordMatchResult(
    bool IsMatch,
    string RecognizedText,
    string NormalizedText,
    WakeWordMatchKind Kind,
    string Detail)
{
    public static WakeWordMatchResult Rejected(
        string recognizedText,
        string normalizedText,
        string detail) =>
        new(false, recognizedText, normalizedText, WakeWordMatchKind.None, detail);

    public static WakeWordMatchResult Accepted(
        string recognizedText,
        string normalizedText,
        WakeWordMatchKind kind,
        string detail) =>
        new(true, recognizedText, normalizedText, kind, detail);
}
