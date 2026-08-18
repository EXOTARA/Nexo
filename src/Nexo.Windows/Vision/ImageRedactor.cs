using System.Drawing;
using System.Drawing.Imaging;
using Nexo.Core.Vision;

namespace Nexo.Windows.Vision;

/// <summary>
/// Diseño D5.4 (extensión) — tapa con un rectángulo sólido las regiones de una captura de pantalla
/// que <see cref="SensitiveContentRedactor.FindSensitiveLines"/> marcó como sensibles, antes de que
/// la imagen se adjunte a cualquier solicitud de IA. Redactar el texto (ya hecho en
/// <see cref="SensitiveContentRedactor"/>) no oculta los píxeles — sin esto, una contraseña visible
/// en pantalla seguiría viajando intacta dentro de la imagen aunque el texto acompañante ya diga
/// "[REDACTADO]". Relleno sólido (no difuminado): un desenfoque puede ser reversible, un rectángulo
/// negro no.
/// </summary>
public static class ImageRedactor
{
    public static byte[] RedactRegions(byte[] pngBytes, IReadOnlyList<OcrTextLine> sensitiveLines)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);
        ArgumentNullException.ThrowIfNull(sensitiveLines);

        if (sensitiveLines.Count == 0)
        {
            return pngBytes;
        }

        // No se capturan aquí los fallos de GDI+ a propósito: si el tapado no se puede completar,
        // devolver la imagen original entregaría intactos justo los píxeles que se querían ocultar.
        // Es preferible que la excepción suba y el comando falle —el llamador ya la traduce a un
        // fallo visible— a enviar en silencio una captura sin redactar a un proveedor de IA.

        using var inputStream = new MemoryStream(pngBytes);
        using var bitmap = new Bitmap(inputStream);

        using (var graphics = Graphics.FromImage(bitmap))
        using (var brush = new SolidBrush(Color.Black))
        {
            foreach (var line in sensitiveLines)
            {
                graphics.FillRectangle(brush, line.Left, line.Top, line.Width, line.Height);
            }
        }

        using var outputStream = new MemoryStream();
        bitmap.Save(outputStream, ImageFormat.Png);
        return outputStream.ToArray();
    }
}
