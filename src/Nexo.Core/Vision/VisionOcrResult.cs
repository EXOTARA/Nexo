namespace Nexo.Core.Vision;

public sealed record VisionOcrResult(
    bool IsSuccess,
    string Detail,
    string Text,
    double Confidence)
{
    public static VisionOcrResult Success(string text, double confidence) =>
        new(
            true,
            string.IsNullOrWhiteSpace(text)
                ? "No encontré texto legible en la imagen."
                : "Texto extraído localmente.",
            text?.Trim() ?? string.Empty,
            Math.Clamp(confidence, 0, 1));

    public static VisionOcrResult Failed(string detail) =>
        new(false, detail, string.Empty, 0);
}
