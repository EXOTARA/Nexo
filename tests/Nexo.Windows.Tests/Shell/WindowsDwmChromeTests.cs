using Nexo.Core.AdaptiveEngine;
using Nexo.Core.Shell;
using Nexo.Windows.Shell;
using Xunit;

namespace Nexo.Windows.Tests.Shell;

/// <summary>
/// Diseño D62 — la sonda que lee del sistema, contra el sistema de verdad.
///
/// La decisión ya está probada aparte con valores inventados; lo que no se puede probar así es que
/// las dos lecturas de Windows —la compilación y los dos ajustes que mandan sobre el desenfoque—
/// devuelvan algo utilizable en una máquina real. Un fallo aquí no daría excepción: daría una
/// compilación cero, y con ella una política que decide "esta versión no puede" en todas partes.
/// </summary>
public sealed class WindowsDwmChromeTests
{
    [Fact]
    public void TheProbeReportsARealWindowsBuild()
    {
        var probe = WindowsDwmChrome.ReadProbe(HardwarePerformanceMode.Balanced);

        // Windows 10 salió con la 10240 y no hay forma de estar ejecutando esto por debajo de ahí.
        Assert.True(
            probe.WindowsBuild >= 10240,
            $"La compilación leída ({probe.WindowsBuild}) no puede ser de un Windows que ejecute .NET 10.");
    }

    [Fact]
    public void TheProbeCarriesTheModeItWasGiven()
    {
        var probe = WindowsDwmChrome.ReadProbe(HardwarePerformanceMode.Eco);

        Assert.Equal(HardwarePerformanceMode.Eco, probe.PerformanceMode);
    }

    [Fact]
    public void ApplyingToNoWindowIsRefusedInsteadOfCrashing()
    {
        // Una ventana sin identificador es lo normal antes de SourceInitialized. Pedirle a DWM que
        // componga la ventana cero no falla: no hace nada, y quien llama tiene que enterarse de que
        // no hay fondo del sistema para pintar el suyo.
        var decision = new WindowBackdropDecision(
            WindowBackdrop.Acrylic,
            WindowCorner.Round,
            PaintOwnBackground: false,
            "prueba");

        Assert.False(WindowsDwmChrome.TryApply(IntPtr.Zero, decision));
    }
}
