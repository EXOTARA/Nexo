using System.Drawing;
using System.Drawing.Imaging;
using Nexo.Windows.Vision;

namespace Nexo.Windows.Tests.Vision;

/// <summary>
/// Diseño D5.2 (Fase 2 — Sakura Lens) — prueba contra el motor de OCR real de Windows, no un
/// doble: renderiza una imagen con texto conocido y confirma que se reconoce. Si el equipo no
/// tiene ningún paquete de idioma de reconocimiento instalado (opcional en Windows, no viene
/// garantizado en toda instalación), el servicio debe fallar de forma honesta en vez de lanzar —
/// eso es lo que se verifica en ese caso, sin marcar la prueba como fallida por una limitación del
/// entorno ajena al código.
/// </summary>
public sealed class WindowsOcrServiceTests
{
    [Fact]
    public async Task RecognizeAsync_WithNoBytes_FailsWithoutThrowing()
    {
        var service = new WindowsOcrService();

        var result = await service.RecognizeAsync([]);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Detail);
    }

    [Fact]
    public async Task RecognizeAsync_WithGarbageBytes_FailsWithoutThrowing()
    {
        var service = new WindowsOcrService();

        var result = await service.RecognizeAsync([1, 2, 3, 4, 5]);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task RecognizeAsync_WithRenderedText_RecognizesItWhenAnEngineIsAvailable()
    {
        var pngBytes = RenderPngWithText("KOHANA KANBAN");
        var service = new WindowsOcrService();

        var result = await service.RecognizeAsync(pngBytes);

        if (!result.IsSuccess)
        {
            // Entorno sin paquete de idioma de OCR instalado: respuesta honesta, no un crash.
            Assert.NotEmpty(result.Detail);
            return;
        }

        Assert.Contains("KOHANA", result.FullText, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(result.Lines);
        Assert.All(result.Lines, line => Assert.True(line.Width > 0 && line.Height > 0));
    }

    private static byte[] RenderPngWithText(string text)
    {
        using var bitmap = new Bitmap(400, 120, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.White);
            using var font = new Font("Arial", 28, System.Drawing.FontStyle.Bold);
            graphics.DrawString(text, font, Brushes.Black, new PointF(10, 30));
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }
}
