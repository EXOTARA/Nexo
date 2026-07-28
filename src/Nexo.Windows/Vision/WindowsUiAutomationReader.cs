using System.Windows.Automation;
using Nexo.Core.Vision;

namespace Nexo.Windows.Vision;

/// <summary>
/// Diseño D5.3 (Fase 2 — Kohana Lens) — implementación real de <see cref="IUiAutomationReader"/>
/// sobre <c>System.Windows.Automation</c> (cliente de UI Automation, no específico de WPF: lee
/// cualquier ventana de Windows). Recorrido acotado a propósito (<see cref="MaxElements"/>,
/// <see cref="MaxDepth"/>): el árbol de una aplicación ajena (un navegador, un IDE) puede tener
/// miles de elementos, y esto es solo lectura de contexto para Lens, no un inventario exhaustivo.
/// </summary>
public sealed class WindowsUiAutomationReader : IUiAutomationReader
{
    private const int MaxElements = 300;
    private const int MaxDepth = 6;

    private static readonly HashSet<string> ContentControlTypes =
    [
        "ControlType.Button",
        "ControlType.Edit",
        "ControlType.Text",
        "ControlType.Hyperlink",
        "ControlType.MenuItem",
        "ControlType.ListItem",
        "ControlType.CheckBox",
        "ControlType.RadioButton",
        "ControlType.ComboBox",
        "ControlType.TabItem"
    ];

    public UiAutomationSnapshot Read(long windowHandle)
    {
        var handle = new IntPtr(windowHandle);
        if (handle == IntPtr.Zero)
        {
            return UiAutomationSnapshot.Failed("No hay ninguna ventana que leer.");
        }

        AutomationElement root;
        string windowTitle;
        try
        {
            root = AutomationElement.FromHandle(handle);
            if (root is null)
            {
                return UiAutomationSnapshot.Failed("La ventana ya no está disponible.");
            }

            windowTitle = root.Current.Name ?? string.Empty;
        }
        catch (Exception exception) when (
            exception is ElementNotAvailableException or ArgumentException or InvalidOperationException)
        {
            return UiAutomationSnapshot.Failed("La ventana ya no está disponible.");
        }

        var elements = new List<UiAutomationElement>();
        Walk(root, TreeWalker.ControlViewWalker, depth: 0, elements);

        return UiAutomationSnapshot.Success(windowTitle, elements);
    }

    private static void Walk(
        AutomationElement element,
        TreeWalker walker,
        int depth,
        List<UiAutomationElement> elements)
    {
        if (elements.Count >= MaxElements || depth > MaxDepth)
        {
            return;
        }

        AutomationElement? child;
        try
        {
            child = walker.GetFirstChild(element);
        }
        catch (Exception exception) when (
            exception is ElementNotAvailableException or InvalidOperationException)
        {
            return;
        }

        while (child is not null && elements.Count < MaxElements)
        {
            TryCollect(child, elements);
            Walk(child, walker, depth + 1, elements);

            try
            {
                child = walker.GetNextSibling(child);
            }
            catch (Exception exception) when (
                exception is ElementNotAvailableException or InvalidOperationException)
            {
                break;
            }
        }
    }

    private static void TryCollect(AutomationElement element, List<UiAutomationElement> elements)
    {
        try
        {
            var current = element.Current;
            var name = string.IsNullOrWhiteSpace(current.Name) ? null : current.Name;
            var controlType = current.ControlType?.ProgrammaticName ?? "ControlType.Unknown";

            if (name is null && !ContentControlTypes.Contains(controlType))
            {
                return;
            }

            var bounds = current.BoundingRectangle;
            if (bounds.IsEmpty || double.IsInfinity(bounds.Width) || double.IsInfinity(bounds.Height))
            {
                return;
            }

            elements.Add(new UiAutomationElement(
                name,
                controlType,
                (int)Math.Round(bounds.Left),
                (int)Math.Round(bounds.Top),
                (int)Math.Round(bounds.Width),
                (int)Math.Round(bounds.Height)));
        }
        catch (ElementNotAvailableException)
        {
            // El elemento desapareció entre enumerarlo y leer sus propiedades: se omite, no se
            // falla toda la lectura por un elemento transitorio de la ventana ajena.
        }
    }
}
