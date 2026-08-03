using System.Windows.Automation;
using Nexo.Core.ComputerUse;
using Nexo.Core.Vision;

namespace Nexo.Windows.ComputerUse;

/// <summary>
/// Diseño D19 (Fase 7, escalón 5) — pulsa un control de una ventana ajena por UI Automation.
///
/// **Vuelve a resolver el control contra el árbol de AHORA**, no contra lo que se leyó al proponer.
/// Entre proponer y confirmar la ventana pudo cambiar —un diálogo que aparece, una lista que se
/// reordena— y pulsar por una posición leída antes es cómo se acaba pulsando otra cosa. Es la
/// diferencia entre invocar un control y hacer clic en unas coordenadas, que es justo lo que este
/// escalón existe para evitar.
///
/// La decisión de si se puede pulsar la toma <see cref="UiAutomationInvokePolicy"/>: aquí se
/// resuelve, se comprueba y se invoca.
/// </summary>
public sealed class WindowsUiAutomationInvoker : IUiAutomationInvoker
{
    private readonly IUiAutomationReader _reader;

    public WindowsUiAutomationInvoker(IUiAutomationReader reader)
    {
        _reader = reader;
    }

    public ComputerUseStepResult Invoke(long windowHandle, string controlName)
    {
        // 1. Releer la ventana y decidir con la política. Si hay ambigüedad, no se pulsa.
        var snapshot = _reader.Read(windowHandle);
        var verdict = UiAutomationInvokePolicy.Decide(snapshot, controlName);
        if (!verdict.IsAllowed)
        {
            return ComputerUseStepResult.Failed(verdict.Message);
        }

        var handle = new IntPtr(windowHandle);
        if (handle == IntPtr.Zero)
        {
            return ComputerUseStepResult.Failed("Esa ventana ya no existe.");
        }

        try
        {
            var root = AutomationElement.FromHandle(handle);
            var condition = new PropertyCondition(
                AutomationElement.NameProperty, verdict.Target!.Name, PropertyConditionFlags.IgnoreCase);

            var matches = root.FindAll(TreeScope.Descendants, condition);

            // 2. La ambigüedad se comprueba OTRA VEZ aquí, sobre el árbol real. La política ya la
            // miró sobre la lectura acotada del lector (que tiene tope de elementos y de
            // profundidad); esta comprobación ve el árbol completo, así que puede encontrar
            // duplicados que aquélla no vio. Ante duda, no se pulsa.
            if (matches.Count == 0)
            {
                return ComputerUseStepResult.Failed(
                    $"«{controlName}» ya no está en esa ventana, así que no pulsé nada.");
            }

            if (matches.Count > 1)
            {
                return ComputerUseStepResult.Failed(
                    $"Ahora hay {matches.Count} controles llamados «{controlName}» y no sé cuál quieres.");
            }

            var element = matches[0];
            if (!element.TryGetCurrentPattern(InvokePattern.Pattern, out var pattern))
            {
                return ComputerUseStepResult.Failed(
                    $"«{controlName}» no acepta que lo pulse desde aquí.");
            }

            ((InvokePattern)pattern).Invoke();
            return ComputerUseStepResult.Ok($"Pulsé «{controlName}».");
        }
        catch (ElementNotAvailableException)
        {
            return ComputerUseStepResult.Failed("La ventana se cerró mientras lo intentaba.");
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException or
                System.Runtime.InteropServices.COMException)
        {
            return ComputerUseStepResult.Failed($"No pude pulsar «{controlName}»: {exception.Message}");
        }
    }
}
