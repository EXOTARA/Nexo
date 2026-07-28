using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Nexo.Core.Vision;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace Nexo.Windows.Vision;

/// <summary>
/// Diseño D5.2 (Fase 2 — Kohana Lens) — implementación real de <see cref="IOcrService"/> sobre
/// <c>Windows.Media.Ocr</c>, desbloqueada por la migración de TFM a
/// <c>net10.0-windows10.0.26100.0</c> (D5.1 / ADR 0003). Nativo de Windows: sin modelo que
/// descargar, sin dependencia nueva.
/// </summary>
public sealed class WindowsOcrService : IOcrService
{
    public async Task<Nexo.Core.Vision.OcrResult> RecognizeAsync(byte[] pngBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);

        if (pngBytes.Length == 0)
        {
            return Nexo.Core.Vision.OcrResult.Failed("No hay ninguna imagen que reconocer.");
        }

        try
        {
            using var softwareBitmap = await DecodeAsync(pngBytes);
            cancellationToken.ThrowIfCancellationRequested();

            var engine = OcrEngine.TryCreateFromUserProfileLanguages()
                ?? TryCreateForLanguage("es")
                ?? TryCreateForLanguage("en");

            if (engine is null)
            {
                return Nexo.Core.Vision.OcrResult.Failed(
                    "Windows no tiene ningún idioma de reconocimiento de texto instalado.");
            }

            var ocrResult = await engine.RecognizeAsync(softwareBitmap);
            var lines = ocrResult.Lines
                .Select(ToTextLine)
                .Where(line => !string.IsNullOrWhiteSpace(line.Text))
                .ToArray();

            return Nexo.Core.Vision.OcrResult.Success(ocrResult.Text, lines);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is COMException or ArgumentException or InvalidOperationException)
        {
            return Nexo.Core.Vision.OcrResult.Failed($"No se pudo reconocer el texto: {exception.Message}");
        }
    }

    private static async Task<SoftwareBitmap> DecodeAsync(byte[] pngBytes)
    {
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(pngBytes.AsBuffer());
        stream.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(stream);
        return await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied);
    }

    private static OcrEngine? TryCreateForLanguage(string languageTag)
    {
        try
        {
            return OcrEngine.TryCreateFromLanguage(new Language(languageTag));
        }
        catch (Exception exception) when (exception is ArgumentException or COMException)
        {
            return null;
        }
    }

    private static OcrTextLine ToTextLine(OcrLine line)
    {
        if (line.Words.Count == 0)
        {
            return new OcrTextLine(line.Text, 0, 0, 0, 0);
        }

        var left = line.Words.Min(word => word.BoundingRect.X);
        var top = line.Words.Min(word => word.BoundingRect.Y);
        var right = line.Words.Max(word => word.BoundingRect.X + word.BoundingRect.Width);
        var bottom = line.Words.Max(word => word.BoundingRect.Y + word.BoundingRect.Height);

        return new OcrTextLine(
            line.Text,
            (int)Math.Round(left),
            (int)Math.Round(top),
            (int)Math.Round(right - left),
            (int)Math.Round(bottom - top));
    }
}
