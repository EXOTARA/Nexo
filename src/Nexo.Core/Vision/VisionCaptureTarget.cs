namespace Nexo.Core.Vision;

public sealed record VisionCaptureTarget(
    string Id,
    long NativeHandle,
    string Title,
    string Subtitle,
    VisionCaptureKind Kind,
    int Left,
    int Top,
    int Width,
    int Height,
    bool IsSensitive = false)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Subtitle)
        ? Title
        : $"{Title} · {Subtitle}";
}
