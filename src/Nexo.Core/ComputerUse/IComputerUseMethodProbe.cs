namespace Nexo.Core.ComputerUse;

/// <summary>
/// Diseño D17 — qué métodos existen DE VERDAD en este equipo ahora mismo.
///
/// Existe como interfaz separada de la política porque son dos preguntas distintas y confundirlas es
/// como se acaba prometiendo lo que no hay: "¿cuál es el más seguro?" la responde
/// <see cref="ComputerUseMethodPolicy"/> y no cambia nunca; "¿cuál está disponible?" depende del
/// equipo, de la aplicación de destino y de lo que Kohana tenga implementado hoy.
///
/// Un método que Kohana no sepa ejecutar **no se declara disponible**, aunque exista en Windows.
/// Declararlo llevaría a elegirlo y fallar después, que es peor que no ofrecerlo.
/// </summary>
public interface IComputerUseMethodProbe
{
    IReadOnlyList<ComputerUseMethod> GetAvailableMethods(string? targetApp);
}
