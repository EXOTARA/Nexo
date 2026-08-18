using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Nexo.Core.Metrics;

namespace Nexo.App.Views.Controls;

/// <summary>
/// Diseño D43 — el medidor pequeño del panel: un anillo con el icono de la pieza dentro y ningún
/// número.
///
/// Es el resumen de un vistazo, no la ficha: quien quiera la cifra exacta tiene la pestaña de
/// Rendimiento a un clic. Meter aquí el porcentaje obligaría a un texto de nueve píxeles que nadie
/// lee y que además compite con el icono por el mismo centro.
///
/// Comparte la trigonometría con <see cref="MetricCapsule"/> a través de <see cref="RingGauge"/>.
/// </summary>
public sealed class IconRing : Control
{
    static IconRing()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(IconRing),
            new FrameworkPropertyMetadata(typeof(IconRing)));
    }

    public static readonly DependencyProperty IconDataProperty =
        DependencyProperty.Register(nameof(IconData), typeof(Geometry), typeof(IconRing));

    /// <summary>
    /// Lado de la caja del medidor. Lo fija la plantilla, y de ahí sale la geometría del arco: la
    /// alternativa —una constante en el código y un tamaño en el XAML— es la que dejó el arco
    /// descolocado en cuanto las tarjetas cambiaron de tamaño.
    /// </summary>
    public static readonly DependencyProperty GaugeSizeProperty =
        DependencyProperty.Register(
            nameof(GaugeSize),
            typeof(double),
            typeof(IconRing),
            new PropertyMetadata(52d, OnGaugeShapeChanged));

    public static readonly DependencyProperty GaugeThicknessProperty =
        DependencyProperty.Register(
            nameof(GaugeThickness),
            typeof(double),
            typeof(IconRing),
            new PropertyMetadata(5d, OnGaugeShapeChanged));

    public double GaugeSize
    {
        get => (double)GetValue(GaugeSizeProperty);
        set => SetValue(GaugeSizeProperty, value);
    }

    public double GaugeThickness
    {
        get => (double)GetValue(GaugeThicknessProperty);
        set => SetValue(GaugeThicknessProperty, value);
    }

    private static void OnGaugeShapeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) =>
        ((IconRing)sender).RedrawGauge();

    public static readonly DependencyProperty PercentProperty =
        DependencyProperty.Register(
            nameof(Percent),
            typeof(double?),
            typeof(IconRing),
            new PropertyMetadata(null, OnPercentChanged));

    public Geometry? IconData
    {
        get => (Geometry?)GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    public double? Percent
    {
        get => (double?)GetValue(PercentProperty);
        set => SetValue(PercentProperty, value);
    }

    private Path? _arc;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _arc = GetTemplateChild("PART_Arc") as Path;
        RedrawGauge();
    }

    private static void OnPercentChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) =>
        ((IconRing)sender).RedrawGauge();

    private void RedrawGauge()
    {
        if (_arc is null)
        {
            return;
        }

        var radius = RingGauge.TrackRadius(GaugeSize, GaugeThickness);
        var centre = new GaugePoint(GaugeSize / 2, GaugeSize / 2);
        var arc = RingGauge.Describe(Percent ?? 0, radius, centre);
        if (arc.IsEmpty)
        {
            _arc.Data = null;
            return;
        }

        var figure = new PathFigure
        {
            StartPoint = new Point(arc.Start.X, arc.Start.Y),
            IsClosed = false
        };

        figure.Segments.Add(new ArcSegment(
            new Point(arc.End.X, arc.End.Y),
            new Size(radius, radius),
            rotationAngle: 0,
            isLargeArc: arc.IsLargeArc,
            sweepDirection: SweepDirection.Clockwise,
            isStroked: true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();

        _arc.Data = geometry;
    }
}
