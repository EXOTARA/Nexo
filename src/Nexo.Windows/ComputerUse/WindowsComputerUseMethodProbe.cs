using Nexo.Core.ComputerUse;

namespace Nexo.Windows.ComputerUse;

/// <summary>
/// Diseño D17 (Fase 7) — qué métodos puede usar Sakura hoy, de verdad.
///
/// **Diseño D19 — UI Automation sube a la lista**, ahora que existe
/// <see cref="WindowsUiAutomationInvoker"/>. Es la demostración de que la escalera funciona sola:
/// no hizo falta tocar la política ni el coordinador para que un método más seguro pase a elegirse
/// antes que el shell y el portapapeles. Basta con que exista.
///
/// La respuesta honesta hoy son tres: UI Automation, la lista corta de comandos de solo lectura y el
/// portapapeles. Los cuatro de arriba (API oficial, App Actions, MCP, integración nativa) **no se
/// declaran disponibles porque Sakura todavía no sabe ejecutarlos**, y declarar un método que no
/// existe llevaría a elegirlo y fallar después — peor que no ofrecerlo. Ratón y teclado simulados
/// tampoco están implementados, y son el último escalón a propósito.
///
/// Que la lista siga siendo corta no es un defecto del diseño: es lo que hace que el orden de
/// preferencia signifique algo.
/// </summary>
public sealed class WindowsComputerUseMethodProbe : IComputerUseMethodProbe
{
    public IReadOnlyList<ComputerUseMethod> GetAvailableMethods(string? targetApp) =>
    [
        ComputerUseMethod.UiAutomation,
        ComputerUseMethod.ShellSeguro,
        ComputerUseMethod.Portapapeles
    ];
}
