namespace Nexo.Core.ComputerUse;

/// <summary>
/// Diseño D17 (Fase 7 — Safe Computer Use) — **el orden es la política**. El roadmap fija esta
/// preferencia y explica por qué: *"automatizar mouse/teclado es frágil e inseguro si se usa como
/// primera opción en vez de último recurso — de ahí el orden estricto"*.
///
/// Los valores están numerados de forma que **menor es más seguro**, para que elegir el mejor método
/// sea comparar números y no recordar una lista. Un orden que hay que recordar es un orden que
/// alguien se salta.
/// </summary>
public enum ComputerUseMethod
{
    /// <summary>API oficial de la aplicación. Lo más seguro: contrato público y estable.</summary>
    ApiOficial = 1,

    /// <summary>Windows App Actions: acciones declaradas por la propia aplicación.</summary>
    AppActions = 2,

    /// <summary>Un servidor MCP que expone la acción de forma tipada.</summary>
    Mcp = 3,

    /// <summary>Integración nativa escrita a propósito para esa aplicación.</summary>
    IntegracionNativa = 4,

    /// <summary>UI Automation: la aplicación expone sus controles y se invoca el correcto.</summary>
    UiAutomation = 5,

    /// <summary>Shell acotado: comandos de una lista de permitidos, nada arbitrario.</summary>
    ShellSeguro = 6,

    /// <summary>Portapapeles: Kohana deja el contenido y la persona lo pega donde quiera.</summary>
    Portapapeles = 7,

    /// <summary>
    /// Ratón y teclado simulados. **Último recurso**, y el roadmap lo dice con todas las letras. No
    /// distingue una ventana de otra, no sabe si algo se movió y no puede comprobar qué hizo.
    /// </summary>
    RatonTeclado = 8
}

public static class ComputerUseMethodText
{
    public static string Describe(ComputerUseMethod method) => method switch
    {
        ComputerUseMethod.ApiOficial => "la API oficial de la aplicación",
        ComputerUseMethod.AppActions => "una acción declarada por la aplicación (App Actions)",
        ComputerUseMethod.Mcp => "un servidor MCP",
        ComputerUseMethod.IntegracionNativa => "una integración nativa",
        ComputerUseMethod.UiAutomation => "los controles que la aplicación expone (UI Automation)",
        ComputerUseMethod.ShellSeguro => "un comando de la lista de permitidos",
        ComputerUseMethod.Portapapeles => "el portapapeles, para que lo pegues tú",
        ComputerUseMethod.RatonTeclado => "ratón y teclado simulados",
        _ => method.ToString()
    };

    /// <summary>Por qué ese método y no otro. Se enseña siempre: elegir sin explicar es pedir fe.</summary>
    public static string WhyItIsSafe(ComputerUseMethod method) => method switch
    {
        ComputerUseMethod.ApiOficial => "Es un contrato público: hace lo que dice y avisa si falla.",
        ComputerUseMethod.AppActions => "La propia aplicación declara qué se le puede pedir.",
        ComputerUseMethod.Mcp => "La acción viaja tipada, no como texto que haya que interpretar.",
        ComputerUseMethod.IntegracionNativa => "Está escrita a propósito para esa aplicación.",
        ComputerUseMethod.UiAutomation => "Se invoca el control concreto, no una posición de pantalla.",
        ComputerUseMethod.ShellSeguro => "Solo comandos de una lista corta que no cambian nada.",
        ComputerUseMethod.Portapapeles => "Kohana no toca la otra aplicación: pegas tú.",
        ComputerUseMethod.RatonTeclado =>
            "No es seguro: no distingue ventanas ni puede comprobar qué hizo. Solo como último recurso.",
        _ => string.Empty
    };
}
