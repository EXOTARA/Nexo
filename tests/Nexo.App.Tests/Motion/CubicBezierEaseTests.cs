using Nexo.App.Motion;
using Xunit;

namespace Nexo.App.Tests.Motion;

/// <summary>
/// La curva se resuelve numéricamente (Newton-Raphson con bisección de respaldo), y un solucionador
/// que converge mal no falla: devuelve valores casi correctos y produce un movimiento que se siente
/// raro sin que nadie pueda decir por qué. Estas pruebas fijan las propiedades que sí se pueden
/// afirmar de una curva de aceleración, en vez de comparar contra números copiados de otra
/// implementación.
/// </summary>
public sealed class CubicBezierEaseTests
{
    private static CubicBezierEase Emphasized() =>
        new() { X1 = 0.2, Y1 = 0, X2 = 0, Y2 = 1 };

    private static CubicBezierEase Spring() =>
        new() { X1 = 0.34, Y1 = 1.56, X2 = 0.64, Y2 = 1 };

    private static CubicBezierEase Linear() =>
        new() { X1 = 0, Y1 = 0, X2 = 1, Y2 = 1 };

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    public void TheCurveAlwaysPassesThroughItsEndpoints(double time)
    {
        // Si los extremos no son exactos, un panel acaba a un píxel de su sitio o entra al 99,7 %
        // de opacidad. Nunca se ve el fallo; se ve la suciedad.
        Assert.Equal(time, Emphasized().Ease(time), precision: 10);
        Assert.Equal(time, Spring().Ease(time), precision: 10);
    }

    [Fact]
    public void TimeOutsideZeroToOneIsClampedInsteadOfExtrapolated()
    {
        var curve = Emphasized();

        Assert.Equal(0, curve.Ease(-0.5));
        Assert.Equal(1, curve.Ease(1.5));
    }

    [Fact]
    public void ALinearControlPolygonReproducesTheIdentity()
    {
        // El caso degenerado con respuesta conocida: es la única forma de comprobar el
        // solucionador contra una verdad independiente y no contra sí mismo.
        var curve = Linear();

        for (var time = 0.0; time <= 1.0; time += 0.05)
        {
            Assert.Equal(time, curve.Ease(time), precision: 5);
        }
    }

    [Fact]
    public void TheEmphasizedCurveNeverGoesBackwards()
    {
        // Monotonía. Una curva de transición que retrocede hace que un panel en movimiento dé un
        // tirón hacia atrás a mitad de camino.
        var curve = Emphasized();
        var previous = double.NegativeInfinity;

        for (var time = 0.0; time <= 1.0; time += 0.01)
        {
            var value = curve.Ease(time);
            Assert.True(
                value >= previous - 1e-9,
                $"La curva retrocedió en t={time:0.00}: {value} venía después de {previous}.");
            previous = value;
        }
    }

    [Fact]
    public void TheEmphasizedCurveRunsAheadOfTheClockAndSettlesLate()
    {
        // Su razón de existir: cubre la mayor parte del recorrido pronto y dedica el resto del
        // tiempo a asentarse. Eso es lo que hace que un panel se lea mientras todavía se mueve, en
        // vez de aparecer ya quieto. Si avanzara al ritmo del reloj sería una recta, y entonces los
        // puntos de control no estarían haciendo nada.
        var curve = Emphasized();

        Assert.True(
            curve.Ease(0.25) > 0.5,
            "Al cuarto del tiempo ya debería llevar más de la mitad del recorrido.");
        Assert.True(
            curve.Ease(0.75) > 0.95,
            "A tres cuartos del tiempo debería estar prácticamente en su sitio, solo asentándose.");
        Assert.True(
            curve.Ease(0.75) < 1.0,
            "Pero no debe haber llegado del todo: el asentamiento final es parte del movimiento.");
    }

    [Fact]
    public void TheSpringCurveOvershootsItsTarget()
    {
        // El rebote es el rasgo, no un efecto secundario: si nunca pasa de 1, no hay resorte.
        var curve = Spring();
        var maximum = 0.0;

        for (var time = 0.0; time <= 1.0; time += 0.005)
        {
            maximum = Math.Max(maximum, curve.Ease(time));
        }

        Assert.True(
            maximum > 1.0,
            $"El resorte nunca se pasó del destino; el máximo fue {maximum:0.0000}.");
    }

    [Fact]
    public void TheAccelerateCurveStaysBehindTheClockThroughout()
    {
        // Curva de salida: sale despacio y se va acelerando. Si adelantara al reloj en algún punto,
        // lo que se retira daría un salto al empezar, que es justo lo contrario de lo que hace falta.
        var curve = new CubicBezierEase { X1 = 0.3, Y1 = 0, X2 = 0.8, Y2 = 0.15 };

        for (var time = 0.05; time <= 0.95; time += 0.05)
        {
            Assert.True(
                curve.Ease(time) < time,
                $"En t={time:0.00} la curva de salida ya iba por delante del reloj.");
        }
    }

    [Fact]
    public void HorizontalControlPointsOutsideTheUnitRangeAreClamped()
    {
        // X fuera de [0,1] describiría una curva que retrocede en el tiempo: no es una función, y el
        // solucionador no tendría una única respuesta. Se recorta en vez de devolver ruido.
        var curve = new CubicBezierEase { X1 = -3, Y1 = 0, X2 = 4, Y2 = 1 };

        for (var time = 0.0; time <= 1.0; time += 0.05)
        {
            var value = curve.Ease(time);
            Assert.False(double.IsNaN(value), $"La curva devolvió NaN en t={time:0.00}.");
            Assert.InRange(value, -0.001, 1.001);
        }
    }
}
