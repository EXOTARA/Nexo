using System.Drawing;
using System.Drawing.Imaging;
using Nexo.Core.Vision;
using Nexo.Windows.Vision;

namespace Nexo.Windows.Tests.Vision;

/// <summary>
/// Diseño D5.4 (extensión) — prueba de punta a punta contra el motor real de OCR: redacta la
/// región de una línea sensible y confirma, volviendo a correr OCR sobre la imagen ya redactada,
/// que el texto sensible ya no es legible. No basta con confiar en que "se dibujó un rectángulo" —
/// se verifica que el resultado deja de contener el secreto.
/// </summary>
public sealed class ImageRedactorTests
{
    /// <summary>
    /// Sin regiones sensibles el método devuelve el MISMO arreglo sin tocar la imagen, así que esta
    /// prueba no necesita renderizar nada: basta con cualquier arreglo no vacío. Antes sí
    /// renderizaba un PNG y resultó ser intermitente (~1 de cada 10 corridas de la suite completa,
    /// nunca aislada): System.Drawing/GDI+ falla de vez en cuando bajo la ejecución en paralelo de
    /// xUnit, compartiendo recursos gráficos con las pruebas de OCR. Quitar el renderizado
    /// innecesario elimina esa fuente de inestabilidad sin debilitar lo que se comprueba — el
    /// contrato de retorno temprano —, en vez de reintentar o silenciar el fallo.
    /// </summary>
    [Fact]
    public void RedactRegions_WithNoSensitiveLines_ReturnsTheSameBytes()
    {
        var original = new byte[] { 1, 2, 3, 4 };

        var result = ImageRedactor.RedactRegions(original, []);

        Assert.Same(original, result);
    }

    [Fact]
    public async Task RedactRegions_BlacksOutTheSensitiveLineSoOcrNoLongerReadsIt()
    {
        var pngBytes = RenderPngWithLines("Usuario: adler", "Password: hunter2secreto");
        var ocrService = new WindowsOcrService();

        var beforeRedaction = await ocrService.RecognizeAsync(pngBytes);
        if (!beforeRedaction.IsSuccess)
        {
            // Entorno sin paquete de idioma de OCR instalado: no hay nada más que verificar aquí.
            return;
        }

        var sensitiveLines = SensitiveContentRedactor.FindSensitiveLines(beforeRedaction);
        Assert.NotEmpty(sensitiveLines);

        var redactedPng = ImageRedactor.RedactRegions(pngBytes, sensitiveLines);
        var afterRedaction = await ocrService.RecognizeAsync(redactedPng);

        Assert.True(afterRedaction.IsSuccess);
        Assert.DoesNotContain("hunter2secreto", afterRedaction.FullText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Usuario", afterRedaction.FullText, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] RenderPngWithLines(params string[] lines)
    {
        using var bitmap = new Bitmap(500, 60 * lines.Length + 40, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.White);
            using var font = new Font("Arial", 22, System.Drawing.FontStyle.Bold);
            for (var i = 0; i < lines.Length; i++)
            {
                graphics.DrawString(lines[i], font, Brushes.Black, new PointF(10, 20 + i * 60));
            }
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }
}
