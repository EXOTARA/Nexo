using System.Windows;
using System.Windows.Media;
using Nexo.Core.Shell;

namespace Nexo.App.Views.Controls;

/// <summary>
/// Diseño D49 — pasa el contorno de flor de <see cref="PetalShape"/> a una geometría de WPF.
///
/// Es lo único que hace: la forma se decide en Nexo.Core, donde se puede probar sin abrir una
/// ventana, y aquí solo se traduce. Las geometrías se congelan y se guardan por tamaño porque las
/// dos carátulas piden la suya en cada apertura del cajón y son ciento veintiocho puntos cada una;
/// reconstruirlas cada vez sería pagar lo mismo para obtener exactamente el mismo objeto.
/// </summary>
internal static class PetalGeometry
{
    private static readonly Dictionary<double, Geometry> Cache = [];

    public static Geometry Create(double size)
    {
        if (Cache.TryGetValue(size, out var cached))
        {
            return cached;
        }

        var radius = size / 2;
        var points = PetalShape.Describe(radius, depth: PetalShape.DepthForSize(size));

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(points[0].X, points[0].Y), isFilled: true, isClosed: true);

            for (var i = 1; i < points.Count; i++)
            {
                context.LineTo(new Point(points[i].X, points[i].Y), isStroked: false, isSmoothJoin: true);
            }
        }

        geometry.Freeze();
        Cache[size] = geometry;
        return geometry;
    }
}
