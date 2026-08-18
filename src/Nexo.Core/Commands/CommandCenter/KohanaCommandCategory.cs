namespace Nexo.Core.Commands.CommandCenter;

/// <summary>
/// Agrupación visible de un comando en el Sakura Command Center. Sirve para ordenar y explicar,
/// no para decidir qué hace el comando.
/// </summary>
public enum KohanaCommandCategory
{
    Navigation,
    Focus,
    Tasks,
    Audio,
    Capture,
    System,
    Shell,
    Ambient
}
