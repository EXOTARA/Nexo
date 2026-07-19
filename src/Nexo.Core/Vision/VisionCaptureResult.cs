namespace Nexo.Core.Vision;

public sealed record VisionCaptureResult(
    bool IsSuccess,
    string Detail,
    string Title,
    byte[]? PngBytes,
    int Width,
    int Height)
{
    public static VisionCaptureResult Success(
        string title,
        byte[] pngBytes,
        int width,
        int height) =>
        new(true, "Captura lista.", title, pngBytes, width, height);

    public static VisionCaptureResult Failed(string detail) =>
        new(false, detail, string.Empty, null, 0, 0);
}
