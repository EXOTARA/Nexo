namespace Nexo.Core.Vision;

public interface IVisionOcrService
{
    Task<VisionOcrResult> RecognizeAsync(
        byte[] pngBytes,
        CancellationToken cancellationToken = default);
}
