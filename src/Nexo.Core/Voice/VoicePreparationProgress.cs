namespace Nexo.Core.Voice;

public sealed record VoicePreparationProgress(string Detail, long BytesDownloaded = 0)
{
    public static VoicePreparationProgress Preparing(string detail) =>
        new(detail, 0);

    public static VoicePreparationProgress Downloading(long bytesDownloaded)
    {
        var safeBytes = Math.Max(0, bytesDownloaded);
        var downloadedMegabytes = safeBytes / 1024d / 1024d;

        return new(
            $"Descargando modelo de voz… {downloadedMegabytes:0} MB",
            safeBytes);
    }
}
