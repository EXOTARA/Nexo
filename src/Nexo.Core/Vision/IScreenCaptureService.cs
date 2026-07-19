namespace Nexo.Core.Vision;

public interface IScreenCaptureService
{
    IReadOnlyList<VisionCaptureTarget> GetAvailableTargets(long excludedWindowHandle = 0);

    Task<VisionCaptureResult> CaptureAsync(
        VisionCaptureTarget target,
        CancellationToken cancellationToken = default);
}
