namespace Nexo.Core.Vision;

/// <summary>
/// Diseño D5.2 — una línea de texto reconocida, con su posición en la imagen capturada (píxeles,
/// origen arriba-izquierda). La posición es la unión de los cuadros de cada palabra de la línea:
/// suficiente para resaltar visualmente sin necesitar palabra por palabra en esta primera versión.
/// </summary>
public sealed record OcrTextLine(
    string Text,
    int Left,
    int Top,
    int Width,
    int Height);
