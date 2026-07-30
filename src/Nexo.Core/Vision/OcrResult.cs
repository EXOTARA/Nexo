namespace Nexo.Core.Vision;

/// <summary>
/// Diseño D5.2 (Fase 2 — Kohana Lens) — resultado de reconocer texto en una captura ya obtenida
/// por <see cref="IScreenCaptureService"/>. Nunca inventa texto: si el motor no encuentra nada,
/// <see cref="Lines"/> queda vacío en vez de rellenarse.
/// </summary>
public sealed record OcrResult(
    bool IsSuccess,
    string Detail,
    string FullText,
    IReadOnlyList<OcrTextLine> Lines)
{
    public static OcrResult Success(string fullText, IReadOnlyList<OcrTextLine> lines) =>
        new(true, "Reconocimiento completo.", fullText, lines);

    public static OcrResult Failed(string detail) =>
        new(false, detail, string.Empty, []);
}
