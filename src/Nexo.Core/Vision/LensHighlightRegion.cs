namespace Nexo.Core.Vision;

/// <summary>
/// Diseño D5.7 (Fase 2 — Sakura Lens) — una región a resaltar en pantalla, en coordenadas
/// absolutas de pantalla (no relativas a la captura ni a la ventana).
/// </summary>
public sealed record LensHighlightRegion(int Left, int Top, int Width, int Height);
