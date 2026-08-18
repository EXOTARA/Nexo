namespace Nexo.Core.ComputerUse;

/// <summary>
/// Diseño D19 — QUÉ se quiere conseguir, aparte de con qué método. Nació de un defecto real que
/// salió al implementar el escalón 5.
///
/// **El defecto:** la regla "no uses un método menos seguro habiendo uno más seguro disponible"
/// comparaba TODOS los métodos disponibles entre sí. En cuanto UI Automation (escalón 5) pasó a
/// estar disponible, el coordinador empezó a rechazar copiar al portapapeles (7) y ejecutar un
/// comando de diagnóstico (6) "porque hay algo más seguro" — cuando UI Automation no sabe hacer
/// ninguna de las dos cosas. La comparación solo significa algo **entre métodos que puedan lograr lo
/// mismo**; entre métodos que hacen cosas distintas es una comparación sin sentido que además rompe
/// funciones que ya estaban.
///
/// La corrección no es relajar la regla, es aplicarla donde tiene sentido: el orden estricto sigue
/// siendo estricto, pero dentro del conjunto de métodos capaces de conseguir ESE objetivo.
/// </summary>
public enum ComputerUseGoal
{
    /// <summary>Pulsar un control de otra aplicación.</summary>
    PulsarControl,

    /// <summary>Dejar un texto disponible para que la persona lo pegue.</summary>
    CopiarTexto,

    /// <summary>Consultar información del equipo sin cambiar nada.</summary>
    ConsultarDiagnostico
}

public static class ComputerUseGoals
{
    /// <summary>
    /// Métodos que PODRÍAN conseguir cada objetivo, existan o no en este equipo. Es una propiedad
    /// del objetivo, no de la máquina: qué hay instalado lo dice
    /// <see cref="IComputerUseMethodProbe"/>, y el conjunto útil es la intersección de los dos.
    /// </summary>
    public static IReadOnlyList<ComputerUseMethod> CapableMethods(ComputerUseGoal goal) => goal switch
    {
        // Pulsar algo admite toda la escalera: desde pedírselo a la aplicación por su API hasta,
        // en el peor caso, mover el ratón. Es justo el objetivo para el que el orden importa.
        ComputerUseGoal.PulsarControl =>
        [
            ComputerUseMethod.ApiOficial,
            ComputerUseMethod.AppActions,
            ComputerUseMethod.Mcp,
            ComputerUseMethod.IntegracionNativa,
            ComputerUseMethod.UiAutomation,
            ComputerUseMethod.RatonTeclado
        ],

        // Copiar es el portapapeles. Simular Ctrl+C sería hacerlo por el escalón 8 lo que el 7 hace
        // directamente, así que no entra.
        ComputerUseGoal.CopiarTexto => [ComputerUseMethod.Portapapeles],

        ComputerUseGoal.ConsultarDiagnostico =>
        [
            ComputerUseMethod.ApiOficial,
            ComputerUseMethod.Mcp,
            ComputerUseMethod.ShellSeguro
        ],

        _ => []
    };

    public static ComputerUseGoal? ForMethod(ComputerUseMethod method) => method switch
    {
        ComputerUseMethod.Portapapeles => ComputerUseGoal.CopiarTexto,
        ComputerUseMethod.ShellSeguro => ComputerUseGoal.ConsultarDiagnostico,
        ComputerUseMethod.UiAutomation or ComputerUseMethod.RatonTeclado => ComputerUseGoal.PulsarControl,
        _ => null
    };

    public static string Describe(ComputerUseGoal goal) => goal switch
    {
        ComputerUseGoal.PulsarControl => "pulsar un control",
        ComputerUseGoal.CopiarTexto => "dejarte algo copiado",
        ComputerUseGoal.ConsultarDiagnostico => "consultar datos del equipo",
        _ => goal.ToString()
    };
}
