using Nexo.Core.Metrics;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D44 — el arco y su carril tienen que ser la misma circunferencia.
///
/// Estas pruebas nacen de un defecto que se vio en pantalla: el arco de relleno flotaba arriba y a
/// la derecha de su carril. La causa no era la trigonometría, que estaba bien, sino que el arco se
/// dibujaba con el radio que decía una constante del código mientras el carril lo trazaba WPF con
/// el tamaño que decía la plantilla. Mientras los dos números coincidieron no se notó; en cuanto
/// las tarjetas cambiaron de tamaño, dejaron de coincidir.
/// </summary>
public sealed class RingGaugeAlignmentTests
{
    [Theory]
    [InlineData(52, 5, 23.5)]
    [InlineData(60, 6, 27)]
    [InlineData(46, 4, 21)]
    public void TrackRadius_IsTheStrokeCentreline(double size, double thickness, double expected) =>
        Assert.Equal(expected, RingGauge.TrackRadius(size, thickness));

    [Fact]
    public void TrackRadius_NeverGoesNegative()
    {
        // Un trazo más grueso que la caja no es un radio negativo, es un medidor que no cabe.
        Assert.Equal(0, RingGauge.TrackRadius(10, 40));
    }

    [Theory]
    [InlineData(52, 5)]
    [InlineData(60, 6)]
    [InlineData(46, 4)]
    public void TheArcStartsAtTwelveOClockOnTheTrack(double size, double thickness)
    {
        var radius = RingGauge.TrackRadius(size, thickness);
        var centre = new GaugePoint(size / 2, size / 2);

        var arc = RingGauge.Describe(35, radius, centre);

        // Las doce en punto de la línea media: centrado en horizontal, y a media anchura de trazo
        // del borde de arriba. Ahí es exactamente donde WPF empieza a pintar la elipse del carril.
        Assert.Equal(size / 2, arc.Start.X, 6);
        Assert.Equal(thickness / 2, arc.Start.Y, 6);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(35)]
    [InlineData(50)]
    [InlineData(78)]
    [InlineData(100)]
    public void EveryArcEndpointSitsExactlyOnTheTrack(double percent)
    {
        const double size = 60;
        const double thickness = 6;

        var radius = RingGauge.TrackRadius(size, thickness);
        var centre = new GaugePoint(size / 2, size / 2);

        var arc = RingGauge.Describe(percent, radius, centre);

        Assert.Equal(radius, Distance(arc.Start, centre), 6);
        Assert.Equal(radius, Distance(arc.End, centre), 6);
    }

    [Fact]
    public void WithoutAnExplicitCentre_TheBoxIsStillTwiceTheRadius()
    {
        // La sobrecarga antigua sigue significando lo mismo, que es lo que la mantiene compatible
        // con quien ya la usaba.
        var arc = RingGauge.Describe(25, 30);

        Assert.Equal(30, arc.Start.X, 6);
        Assert.Equal(0, arc.Start.Y, 6);
    }

    [Fact]
    public void AnEmptyGaugeStillReportsTwelveOClockOnTheTrack()
    {
        // Con cero por ciento no se dibuja nada, pero los puntos no pueden salir del origen de
        // coordenadas: quien los use para colocar un extremo lo pondría en la esquina de la tarjeta.
        var centre = new GaugePoint(30, 30);
        var arc = RingGauge.Describe(0, 27, centre);

        Assert.True(arc.IsEmpty);
        Assert.Equal(30, arc.Start.X, 6);
        Assert.Equal(3, arc.Start.Y, 6);
    }

    private static double Distance(GaugePoint point, GaugePoint centre) =>
        Math.Sqrt(Math.Pow(point.X - centre.X, 2) + Math.Pow(point.Y - centre.Y, 2));
}
