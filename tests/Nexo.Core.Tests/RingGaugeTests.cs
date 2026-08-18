using Nexo.Core.Metrics;
using Xunit;

namespace Nexo.Core.Tests;

public sealed class RingGaugeTests
{
    private const double Radius = 50;
    private const double Tolerance = 0.01;

    [Fact]
    public void TheArcStartsAtTwelveOClockAndNotAtThree()
    {
        // El cero trigonométrico está a las tres. Un medidor que arranque ahí se lee torcido, y es
        // el tipo de error que se acepta sin darse cuenta porque «gira igual».
        var arc = RingGauge.Describe(50, Radius);

        Assert.Equal(Radius, arc.Start.X, Tolerance);
        Assert.Equal(0, arc.Start.Y, Tolerance);
    }

    [Fact]
    public void AQuarterEndsAtThreeOClock()
    {
        var arc = RingGauge.Describe(25, Radius);

        Assert.Equal(Radius * 2, arc.End.X, Tolerance);
        Assert.Equal(Radius, arc.End.Y, Tolerance);
    }

    [Fact]
    public void AHalfEndsAtSixOClock()
    {
        var arc = RingGauge.Describe(50, Radius);

        Assert.Equal(Radius, arc.End.X, Tolerance);
        Assert.Equal(Radius * 2, arc.End.Y, Tolerance);
    }

    [Theory]
    [InlineData(10, false)]
    [InlineData(50, false)]
    [InlineData(51, true)]
    [InlineData(80, true)]
    [InlineData(99, true)]
    public void PastHalfTheArcMustTakeTheLongWayRound(double percent, bool expectedLargeArc)
    {
        // Sin esta bandera el dibujo toma siempre el camino corto, y entonces un 80% se ve idéntico
        // a un 20%: dos lecturas opuestas con la misma imagen. Nadie lo descubre hasta que el equipo
        // está al ochenta por ciento, que es justo cuando hace falta que se lea bien.
        var arc = RingGauge.Describe(percent, Radius);

        Assert.Equal(expectedLargeArc, arc.IsLargeArc);
    }

    [Fact]
    public void FullIsDrawnJustShortOfAWholeTurnSoTheStrokeSurvives()
    {
        // Un arco de 360 grados exactos empieza y acaba en el mismo punto y desaparece. Se recorta
        // un pelo: el círculo se cierra a la vista sin degenerar.
        var arc = RingGauge.Describe(100, Radius);

        Assert.False(arc.IsEmpty);
        Assert.True(arc.IsLargeArc);
        Assert.True(
            Math.Abs(arc.End.X - arc.Start.X) > 0 || Math.Abs(arc.End.Y - arc.Start.Y) > 0,
            "Al 100% el punto final no puede coincidir con el inicial: el trazo se borraría.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(double.NaN)]
    public void NothingToShowIsReportedAsEmptyInsteadOfDrawingGarbage(double percent)
    {
        // El porcentaje puede llegar sin medir: una GPU que no informa uso da null y acaba aquí como
        // cero o NaN. Dibujar un arco degenerado deja un punto suelto en pantalla.
        var arc = RingGauge.Describe(percent, Radius);

        Assert.True(arc.IsEmpty);
    }

    [Fact]
    public void AboveOneHundredIsClampedInsteadOfWrappingAround()
    {
        // Un contador que pase de cien —pasa con el uso de CPU agregado— daría más de una vuelta y
        // el arco volvería a parecer pequeño.
        var full = RingGauge.Describe(100, Radius);
        var beyond = RingGauge.Describe(140, Radius);

        Assert.Equal(full.End.X, beyond.End.X, Tolerance);
        Assert.Equal(full.End.Y, beyond.End.Y, Tolerance);
    }

    [Fact]
    public void EveryPercentStaysOnTheCircle()
    {
        // Propiedad que ata la trigonometría entera: todo punto del arco tiene que estar a exactamente
        // un radio del centro. Un seno y un coseno intercambiados sigue dando puntos «razonables»,
        // pero deja de cumplir esto.
        for (var percent = 1; percent <= 100; percent++)
        {
            var arc = RingGauge.Describe(percent, Radius);
            var dx = arc.End.X - Radius;
            var dy = arc.End.Y - Radius;

            Assert.Equal(Radius, Math.Sqrt((dx * dx) + (dy * dy)), 0.01);
        }
    }

    [Fact]
    public void ARadiusOfZeroIsTreatedAsNothingToDraw()
    {
        Assert.True(RingGauge.Describe(50, 0).IsEmpty);
    }
}
