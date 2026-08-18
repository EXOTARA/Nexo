using Nexo.Core.ComputerUse;
using Nexo.Windows.ComputerUse;

namespace Nexo.Windows.Tests;

/// <summary>
/// Diseño D18 — el ejecutor de comandos, sobre un proceso real. Antes de esto solo se probaba a
/// través de dobles en <c>ComputerUseCoordinatorTests</c>, así que el código que de verdad lanza
/// `Process.Start` nunca se había ejecutado en la suite. La corrección del riesgo de interbloqueo
/// (leer stdout y stderr por eventos en vez de `ReadToEnd()` secuencial) solo tiene sentido si algo
/// la ejerce contra un proceso de Windows de verdad.
///
/// El portapapeles no se prueba aquí: `Clipboard` exige hilo STA y tocaría el portapapeles real del
/// equipo donde corra la prueba, que no es algo que una suite automatizada deba hacer.
/// </summary>
public sealed class WindowsComputerUseExecutorTests
{
    private readonly WindowsComputerUseExecutor _executor = new();

    [Fact]
    public void ARealAllowedCommand_RunsAndCapturesOutput()
    {
        var command = SafeShellCatalog.Find("net.ip")!;

        var result = _executor.RunSafeCommand(command);

        Assert.True(result.Success, result.Detail);
        Assert.False(string.IsNullOrWhiteSpace(result.Output));
    }

    [Fact]
    public void ACommandWithLargerOutput_DoesNotHangOrTruncate()
    {
        // driverquery puede producir varios kilobytes en un equipo con muchos controladores — el
        // caso donde la lectura secuencial antigua podía interbloquearse si el búfer del sistema
        // se llenaba mientras el otro canal seguía sin leerse.
        var command = SafeShellCatalog.Find("sys.drivers")!;

        var result = _executor.RunSafeCommand(command);

        Assert.True(result.Success, result.Detail);
        Assert.False(string.IsNullOrWhiteSpace(result.Output));
    }

    [Fact]
    public void ACommandNotInTheCatalog_IsRefused_AndNeverRuns()
    {
        // Última puerta: aunque alguien construya un SafeShellCommand con datos propios, si no es
        // (por valor) uno de los de la lista fija, no se ejecuta.
        var rogue = new SafeShellCommand("rogue", "Comando inventado", "cmd.exe", "/c echo hola");

        var result = _executor.RunSafeCommand(rogue);

        Assert.False(result.Success);
        Assert.Contains("lista de permitidos", result.Detail);
    }
}
