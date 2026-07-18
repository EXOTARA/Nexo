namespace Nexo.Core.Voice;

public sealed record VoicePreparationResult(bool IsReady, string Detail)
{
    public static VoicePreparationResult Ready(string? detail = null) =>
        new(true, string.IsNullOrWhiteSpace(detail)
            ? "Voz local lista."
            : detail.Trim());

    public static VoicePreparationResult Unavailable(string detail) =>
        new(false, string.IsNullOrWhiteSpace(detail)
            ? "La voz local no está disponible."
            : detail.Trim());
}
