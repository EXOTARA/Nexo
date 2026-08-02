using Nexo.Core.ComputerUse;

namespace Nexo.Windows.ComputerUse;

/// <summary>
/// Diseño D17 (Fase 7) — qué métodos puede usar Kohana hoy, de verdad.
///
/// La respuesta honesta es: dos. El portapapeles y la lista corta de comandos de solo lectura. Los
/// cuatro de arriba (API oficial, App Actions, MCP, integración nativa) **no se declaran
/// disponibles porque Kohana todavía no sabe ejecutarlos**, y declarar un método que no existe
/// llevaría a elegirlo y fallar después — peor que no ofrecerlo. UI Automation ya se usa para LEER
/// (Kohana Lens), pero leer un control e invocarlo no son la misma capacidad, y la de invocar no
/// está escrita.
///
/// Que la lista sea corta no es un defecto del diseño: es lo que hace que el orden de preferencia
/// signifique algo. Cuando existan los métodos de arriba, entrarán aquí y la política los elegirá
/// sola, sin tocar nada más.
/// </summary>
public sealed class WindowsComputerUseMethodProbe : IComputerUseMethodProbe
{
    public IReadOnlyList<ComputerUseMethod> GetAvailableMethods(string? targetApp) =>
    [
        ComputerUseMethod.ShellSeguro,
        ComputerUseMethod.Portapapeles
    ];
}
