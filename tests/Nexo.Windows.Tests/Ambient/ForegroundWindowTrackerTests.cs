using Nexo.Windows.Ambient;

namespace Nexo.Windows.Tests.Ambient;

/// <summary>
/// Diseño D4 (corrección post smoke test) — el hook real de <c>SetWinEventHook</c> depende del
/// entorno (qué ventana tiene foco al correr la prueba), así que estas pruebas solo verifican que
/// construir, consultar y liberar el rastreador es seguro y no lanza — no el valor exacto que
/// devuelve, que no es determinista fuera de una sesión interactiva controlada.
/// </summary>
public sealed class ForegroundWindowTrackerTests
{
    [Fact]
    public void Construction_NeverThrowsAndExposesANonNegativeHandle()
    {
        using var tracker = new ForegroundWindowTracker();

        Assert.True(tracker.LastExternalWindowHandle >= 0);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var tracker = new ForegroundWindowTracker();

        tracker.Dispose();
        tracker.Dispose();
    }
}
