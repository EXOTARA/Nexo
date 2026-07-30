namespace Nexo.Core.Vision;

/// <summary>
/// Diseño D5.3 — resultado de leer el árbol de automatización de una ventana ajena. Los límites de
/// la propia ventana (<see cref="WindowLeft"/>/<see cref="WindowTop"/>/<see cref="WindowWidth"/>/
/// <see cref="WindowHeight"/>, en coordenadas absolutas de pantalla) se agregaron en D5.7: UI
/// Automation ya los da gratis (son el <c>BoundingRectangle</c> del elemento raíz), y son
/// justo lo que necesita el resaltado visual para traducir cajas relativas a la captura
/// (OCR) en posiciones reales de pantalla.
/// </summary>
public sealed record UiAutomationSnapshot(
    bool IsSuccess,
    string Detail,
    string WindowTitle,
    IReadOnlyList<UiAutomationElement> Elements,
    int WindowLeft = 0,
    int WindowTop = 0,
    int WindowWidth = 0,
    int WindowHeight = 0)
{
    public static UiAutomationSnapshot Success(
        string windowTitle,
        IReadOnlyList<UiAutomationElement> elements,
        int windowLeft,
        int windowTop,
        int windowWidth,
        int windowHeight) =>
        new(true, "Lectura completa.", windowTitle, elements, windowLeft, windowTop, windowWidth, windowHeight);

    public static UiAutomationSnapshot Failed(string detail) =>
        new(false, detail, string.Empty, []);
}
