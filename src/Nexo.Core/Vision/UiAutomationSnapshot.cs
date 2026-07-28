namespace Nexo.Core.Vision;

/// <summary>Diseño D5.3 — resultado de leer el árbol de automatización de una ventana ajena.</summary>
public sealed record UiAutomationSnapshot(
    bool IsSuccess,
    string Detail,
    string WindowTitle,
    IReadOnlyList<UiAutomationElement> Elements)
{
    public static UiAutomationSnapshot Success(
        string windowTitle,
        IReadOnlyList<UiAutomationElement> elements) =>
        new(true, "Lectura completa.", windowTitle, elements);

    public static UiAutomationSnapshot Failed(string detail) =>
        new(false, detail, string.Empty, []);
}
