namespace Nexo.Core.Vision;

/// <summary>
/// Diseño D5.3 (Fase 2 — Sakura Lens) — lee, de solo lectura, los elementos visibles de una
/// ventana ajena por su handle nativo: nombre, tipo de control y posición. Complementa el OCR
/// (<see cref="IOcrService"/>): OCR ve texto en píxeles, esto ve la estructura semántica que la
/// propia aplicación expone. Ninguno de los dos ejecuta ninguna acción sobre la ventana leída.
/// </summary>
public interface IUiAutomationReader
{
    UiAutomationSnapshot Read(long windowHandle);
}
