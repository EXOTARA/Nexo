using Nexo.Core.Vision;
using TesseractOCR;
using TesseractImage = TesseractOCR.Pix.Image;
using TesseractOCR.Enums;


namespace Nexo.Windows.Vision;

public sealed class TesseractVisionOcrService : IVisionOcrService
{
    private const string SpanishDataUrl =
        "https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/main/spa.traineddata";

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(3)
    };

    private readonly SemaphoreSlim _preparationGate = new(1, 1);
    private readonly string _tessDataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Nexo",
        "Ocr",
        "tessdata");

    public async Task<VisionOcrResult> RecognizeAsync(
        byte[] pngBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);
        if (pngBytes.Length == 0)
        {
            return VisionOcrResult.Failed("La imagen está vacía.");
        }

        try
        {
            await EnsureLanguageDataAsync(cancellationToken);

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var engine = new Engine(
                    Path.GetDirectoryName(_tessDataDirectory)!,
                    Language.SpanishCastilian,
                    EngineMode.Default);

            using var image = TesseractImage.LoadFromMemory(pngBytes);
            using var page = engine.Process(image);

return VisionOcrResult.Success(
    page.Text,
    page.MeanConfidence);
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DllNotFoundException)
        {
            return VisionOcrResult.Failed(
                "El OCR local necesita Microsoft Visual C++ Runtime x64. Nexo Vision puede seguir usando la imagen sin OCR.");
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException or InvalidOperationException)
        {
            return VisionOcrResult.Failed(
                $"No pude ejecutar el OCR local: {exception.Message}");
        }
    }

    private async Task EnsureLanguageDataAsync(CancellationToken cancellationToken)
    {
        var dataPath = Path.Combine(_tessDataDirectory, "spa.traineddata");
        if (File.Exists(dataPath) && new FileInfo(dataPath).Length > 100_000)
        {
            return;
        }

        await _preparationGate.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(dataPath) && new FileInfo(dataPath).Length > 100_000)
            {
                return;
            }

            Directory.CreateDirectory(_tessDataDirectory);
            var temporaryPath = dataPath + ".download";
            using var response = await HttpClient.GetAsync(
                SpanishDataUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                81920,
                useAsync: true))
            {
                await input.CopyToAsync(output, cancellationToken);
            }

            File.Move(temporaryPath, dataPath, overwrite: true);
        }
        finally
        {
            _preparationGate.Release();
        }
    }
}
