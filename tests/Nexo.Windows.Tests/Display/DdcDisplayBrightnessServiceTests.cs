using Nexo.Windows.Display;
using Xunit;

namespace Nexo.Windows.Tests.Display;

/// <summary>
/// Corre contra el monitor real. Es deliberado: lo que hay que saber —si este monitor responde al
/// canal de control por cable— es justo lo que un doble no puede decir, y es la razón por la que se
/// descartó WMI, que pasaba sus pruebas y devolvía «Incompatible» en el equipo.
///
/// La prueba no exige que haya brillo: en un monitor sin DDC/CI lo correcto es que no lo haya. Lo
/// que se exige es que el servicio lo diga en vez de reventar, y que si dice que sí, el valor tenga
/// sentido.
/// </summary>
public sealed class DdcDisplayBrightnessServiceTests
{
    [Fact]
    public void ReadingBrightnessNeverThrowsAndReportsWhatItFound()
    {
        using var service = new DdcDisplayBrightnessService();

        var snapshot = service.ReadSnapshot();

        if (snapshot.IsAvailable)
        {
            Assert.InRange(snapshot.Percent, 0, 100);
            Assert.Null(snapshot.ErrorMessage);
        }
        else
        {
            // Sin brillo hay que poder explicar por qué; un panel que se omite sin motivo es
            // indistinguible de uno que falló.
            Assert.False(string.IsNullOrWhiteSpace(snapshot.ErrorMessage));
        }
    }

    [Fact]
    public void SettingTheCurrentBrightnessLeavesItWhereItWas()
    {
        // Escribe el valor que ya tenía: comprueba el camino de escritura completo sin cambiarle el
        // brillo a nadie mientras corren las pruebas.
        using var service = new DdcDisplayBrightnessService();

        var before = service.ReadSnapshot();
        if (!before.IsAvailable)
        {
            return;
        }

        Assert.True(service.TrySetBrightness(before.Percent));

        var after = service.ReadSnapshot();
        Assert.True(
            Math.Abs(after.Percent - before.Percent) <= 2,
            $"El brillo pasó de {before.Percent}% a {after.Percent}% al reescribir el mismo valor.");
    }

    [Fact]
    public void AfterDisposingItReportsUnavailableInsteadOfUsingAFreedHandle()
    {
        // Los identificadores de monitor se liberan al cerrar. Usarlos después es memoria liberada,
        // y eso no falla de forma limpia: corrompe.
        var service = new DdcDisplayBrightnessService();
        service.ReadSnapshot();
        service.Dispose();

        Assert.False(service.ReadSnapshot().IsAvailable);
        Assert.False(service.TrySetBrightness(50));
    }
}
