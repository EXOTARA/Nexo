using Nexo.Core.Shell;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D49 — el contorno de flor de la carátula.
///
/// Se prueba la fórmula y no el dibujo: que el contorno cierre, que quepa en su caja y que la
/// ondulación desaparezca sola en los tamaños pequeños son afirmaciones concretas, no un «se ve
/// bien» mirando la pantalla.
/// </summary>
public sealed class PetalShapeTests
{
    [Fact]
    public void TheOutlineStaysInsideItsBox()
    {
        // Si se saliera, la carátula se recortaría contra el borde de su celda y la flor aparecería
        // con los pétalos cortados en plano.
        const double radius = 94;
        var points = PetalShape.Describe(radius);

        var limit = radius * (1 + PetalShape.DefaultDepth);
        Assert.All(points, point =>
        {
            Assert.InRange(point.X, radius - limit, radius + limit);
            Assert.InRange(point.Y, radius - limit, radius + limit);
        });
    }

    [Fact]
    public void EveryPointStaysBetweenTheValleyAndTheCrest()
    {
        const double radius = 94;
        var points = PetalShape.Describe(radius);

        var minimum = radius * (1 - PetalShape.DefaultDepth);
        var maximum = radius * (1 + PetalShape.DefaultDepth);

        Assert.All(points, point =>
        {
            var distance = Math.Sqrt(
                Math.Pow(point.X - radius, 2) + Math.Pow(point.Y - radius, 2));
            Assert.InRange(distance, minimum - 0.001, maximum + 0.001);
        });
    }

    [Fact]
    public void ItStartsAtTwelveOClockOnACrest()
    {
        // Con un número par de pétalos, empezar a las tres dejaría un valle justo arriba y la forma
        // se vería torcida al lado de un anillo que sí empieza arriba.
        const double radius = 94;
        var points = PetalShape.Describe(radius);

        Assert.Equal(radius, points[0].X, 6);
        Assert.Equal(radius - (radius * (1 + PetalShape.DefaultDepth)), points[0].Y, 6);
    }

    [Fact]
    public void TheOutlineHasOneCrestPerPetal()
    {
        const double radius = 100;
        const int petals = 8;
        var points = PetalShape.Describe(radius, petals, samplesPerPetal: 16);

        var distances = points
            .Select(point => Math.Sqrt(Math.Pow(point.X - radius, 2) + Math.Pow(point.Y - radius, 2)))
            .ToArray();

        var crests = 0;
        for (var i = 0; i < distances.Length; i++)
        {
            var previous = distances[(i - 1 + distances.Length) % distances.Length];
            var next = distances[(i + 1) % distances.Length];

            if (distances[i] > previous && distances[i] >= next)
            {
                crests++;
            }
        }

        Assert.Equal(petals, crests);
    }

    [Fact]
    public void SmallSizes_ComeBackAsAPlainCircle()
    {
        // A 46 píxeles la ondulación mediría poco más de un píxel: no se lee como borde vivo, solo
        // ensucia el canto. Ahí la forma correcta es el círculo.
        Assert.Equal(0, PetalShape.DepthForSize(46));
        Assert.Equal(0, PetalShape.DepthForSize(48));
    }

    [Fact]
    public void LargeSizes_GetTheFullDepth() =>
        Assert.Equal(PetalShape.DefaultDepth, PetalShape.DepthForSize(188));

    [Fact]
    public void BetweenTheTwo_TheDepthGrowsSmoothly()
    {
        var small = PetalShape.DepthForSize(80);
        var medium = PetalShape.DepthForSize(120);

        Assert.True(small > 0);
        Assert.True(medium > small);
        Assert.True(medium < PetalShape.DefaultDepth);
    }

    [Fact]
    public void ADepthOfZero_IsExactlyACircle()
    {
        const double radius = 50;
        var points = PetalShape.Describe(radius, depth: 0);

        Assert.All(points, point =>
        {
            var distance = Math.Sqrt(
                Math.Pow(point.X - radius, 2) + Math.Pow(point.Y - radius, 2));
            Assert.Equal(radius, distance, 6);
        });
    }

    [Fact]
    public void ADepthOfOneOrMore_IsRefused()
    {
        // Con profundidad 1 el radio llega a cero en cada valle y el contorno se cruza consigo
        // mismo: deja de ser una silueta y pasa a ser un nudo.
        Assert.Throws<ArgumentOutOfRangeException>(() => PetalShape.Describe(50, depth: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => PetalShape.Describe(50, depth: -0.1));
    }
}
