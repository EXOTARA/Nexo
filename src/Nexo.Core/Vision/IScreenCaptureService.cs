namespace Nexo.Core.Vision;

public interface IScreenCaptureService
{
    IReadOnlyList<VisionCaptureTarget> GetAvailableTargets(long excludedWindowHandle = 0);

    void SetCustomExclusions(IEnumerable<string> exclusions);

    Task<VisionCaptureResult> CaptureAsync(
        VisionCaptureTarget target,
        CancellationToken cancellationToken = default);
}
