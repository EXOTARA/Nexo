using System.Windows;
using System.Windows.Controls;
using Nexo.App.Views.Controls;

namespace Nexo.App.Tests;

/// <summary>
/// Diseño D64 — la píldora se mide, no se adivina.
///
/// El token que había antes valía 999 con la idea de que WPF recortaría el radio al alto
/// disponible. No lo recorta: dibuja los arcos pedidos y deja dos puntas. Esta prueba fija lo
/// contrario de aquella suposición, con un Border de verdad medido por WPF de verdad.
/// </summary>
[Collection(StaWpfCollection.Name)]
public sealed class PillCornerTests
{
    private readonly StaWpfFixture _wpf;

    public PillCornerTests(StaWpfFixture wpf) => _wpf = wpf;

    [Fact]
    public void ABorderMarkedAsPill_GetsHalfItsHeightAsRadius()
    {
        _wpf.Invoke(() =>
        {
            var border = new Border { Width = 220, Height = 40 };
            PillCorner.SetIsPill(border, true);

            Measure(border);

            Assert.Equal(20, border.CornerRadius.TopLeft);
            Assert.Equal(20, border.CornerRadius.BottomRight);
        });
    }

    [Fact]
    public void TheRadiusFollowsTheHeightWhenItChanges()
    {
        // Una insignia de 22 y un botón de 40 comparten el estilo pero no el radio, y ese era
        // justo el caso que un número fijo no podía cubrir.
        _wpf.Invoke(() =>
        {
            var border = new Border { Width = 120, Height = 40 };
            PillCorner.SetIsPill(border, true);
            Measure(border);

            border.Height = 22;
            Measure(border);

            Assert.Equal(11, border.CornerRadius.TopLeft);
        });
    }

    [Fact]
    public void WithoutBeingMeasuredYet_NoRadiusIsForced()
    {
        // Antes de la primera medida el alto es cero; escribir un radio cero ahí solo serviría
        // para pisar lo que el estilo hubiera puesto.
        _wpf.Invoke(() =>
        {
            var border = new Border { CornerRadius = new CornerRadius(14) };
            PillCorner.SetIsPill(border, true);

            Assert.Equal(14, border.CornerRadius.TopLeft);
        });
    }

    private static void Measure(FrameworkElement element)
    {
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        element.Arrange(new Rect(element.DesiredSize));
        element.UpdateLayout();
    }
}
