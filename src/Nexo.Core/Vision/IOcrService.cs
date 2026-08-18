namespace Nexo.Core.Vision;

/// <summary>
/// Diseño D5.2 (Fase 2 — Kohana Lens) — reconoce el texto visible en una captura ya obtenida por
/// <see cref="IScreenCaptureService"/>. Paso separado a propósito: la captura decide QUÉ se mira
/// (con las exclusiones de <see cref="VisionPrivacyPolicy"/> ya aplicadas antes de llegar aquí);
/// este servicio solo extrae texto de una imagen que ya se decidió que es segura de procesar.
/// </summary>
public interface IOcrService
{
    Task<OcrResult> RecognizeAsync(byte[] pngBytes, CancellationToken cancellationToken = default);
}
