using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Nexo.App.Motion;
using Nexo.Core.Metrics;

namespace Nexo.App.Views.Controls;

/// <summary>
/// Diseño D42 — la cápsula de una métrica: título, modelo, un medidor circular y una línea de
/// detalle.
///
/// Es un control y no XAML repetido cinco veces porque el medidor tiene partes móviles —el arco se
/// redibuja con cada lectura, dos veces por segundo— y duplicar eso significaría cinco copias del
/// mismo cálculo, con cinco oportunidades de que una se quede sin actualizar.
///
/// La geometría del arco vive en <see cref="RingGauge"/>, en Nexo.Core, y no aquí: es
/// trigonometría con tres trampas que solo se ven con el porcentaje exacto que las destapa, y eso
/// hay que poder probarlo sin abrir una ventana.
/// </summary>
public sealed class MetricCapsule : Control
{
    static MetricCapsule()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(MetricCapsule),
            new FrameworkPropertyMetadata(typeof(MetricCapsule)));
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(MetricCapsule));

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(MetricCapsule));

    public static readonly DependencyProperty DetailProperty =
        DependencyProperty.Register(nameof(Detail), typeof(string), typeof(MetricCapsule));

    public static readonly DependencyProperty IconDataProperty =
        DependencyProperty.Register(nameof(IconData), typeof(Geometry), typeof(MetricCapsule));

    /// <summary>
    /// Lado de la caja del medidor. Lo fija la plantilla, y de ahí sale la geometría del arco: la
    /// alternativa —una constante en el código y un tamaño en el XAML— es la que dejó el arco
    /// descolocado en cuanto las tarjetas cambiaron de tamaño.
    /// </summary>
    public static readonly DependencyProperty GaugeSizeProperty =
        DependencyProperty.Register(
            nameof(GaugeSize),
            typeof(double),
            typeof(MetricCapsule),
            new PropertyMetadata(52d, OnGaugeShapeChanged));

    public static readonly DependencyProperty GaugeThicknessProperty =
        DependencyProperty.Register(
            nameof(GaugeThickness),
            typeof(double),
            typeof(MetricCapsule),
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
        ((MetricCapsule)sender).RedrawGauge();

    /// <summary>
    /// El porcentaje, o <c>null</c> cuando no hay medida. Null y cero son cosas distintas y se
    /// enseñan distinto: una GPU que no informa uso no está al 0%, es que no se sabe.
    /// </summary>
    public static readonly DependencyProperty PercentProperty =
        DependencyProperty.Register(
            nameof(Percent),
            typeof(double?),
            typeof(MetricCapsule),
            new PropertyMetadata(null, OnPercentChanged));

    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Subtitle
    {
        get => (string?)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public string? Detail
    {
        get => (string?)GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

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
    private TextBlock? _readout;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _arc = GetTemplateChild("PART_Arc") as Path;
        _readout = GetTemplateChild("PART_Readout") as TextBlock;

        RedrawGauge();
    }

    private static void OnPercentChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) =>
        ((MetricCapsule)sender).RedrawGauge();

    private void RedrawGauge()
    {
        if (_readout is not null)
        {
            _readout.Text = Percent is { } value
                ? $"{Math.Round(value)}%"
                : "—";
        }

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
