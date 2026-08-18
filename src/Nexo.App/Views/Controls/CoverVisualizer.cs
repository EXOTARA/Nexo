using System.Windows;
using System.Windows.Media;
using Nexo.Core.Media;

namespace Nexo.App.Views.Controls;

/// <summary>
/// Diseño D46 — el anillo de rayos que rodea la carátula, como en la referencia.
///
/// Dibuja con <see cref="OnRender"/> y no con formas del árbol visual. Son sesenta rayos que
/// cambian treinta veces por segundo: como <c>Line</c> serían sesenta elementos con su medida, su
/// disposición y su recorrido de propiedades en cada fotograma, y eso se nota en un equipo modesto.
/// Un solo <see cref="StreamGeometry"/> por fotograma no.
///
/// Los rayos tienen un largo mínimo aunque no suene nada: sin él, el anillo desaparece del todo en
/// los silencios y la carátula parece perder su marco cada dos por tres. Con él, el marco siempre
/// está y lo que cambia es cuánto respira.
/// </summary>
public sealed class CoverVisualizer : FrameworkElement
{
    /// <summary>
    /// Diseño D60 — **bandas**, no rayos. El anillo dibuja el doble: cada banda se pinta dos veces,
    /// una en cada mitad, para que el aro sea simétrico.
    ///
    /// D50 bajó «de 128 rayos a 64» y dejó este número en 64, que son 128 rayos otra vez: el
    /// arreglo no llegó a aplicarse y el anillo siguió tan tupido como antes. En una circunferencia
    /// de unos 580 píxeles, 128 trazos de 4,5 de grosor no dejan hueco entre vecinos y el aro deja
    /// de leerse como rayos para leerse como un borde peludo.
    ///
    /// 32 bandas siguen siendo más de cuatro por octava en el rango que se representa, que es más
    /// de lo que el ojo distingue en un anillo.
    /// </summary>
    private const int DefaultBandCount = 32;

    private double[] _levels = new double[DefaultBandCount];
    private Pen? _pen;

    public static readonly DependencyProperty BarBrushProperty =
        DependencyProperty.Register(
            nameof(BarBrush),
            typeof(Brush),
            typeof(CoverVisualizer),
            new FrameworkPropertyMetadata(
                Brushes.White,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnPenChanged));

    /// <summary>Radio donde arrancan los rayos, medido desde el centro del control.</summary>
    public static readonly DependencyProperty InnerRadiusProperty =
        DependencyProperty.Register(
            nameof(InnerRadius),
            typeof(double),
            typeof(CoverVisualizer),
            new FrameworkPropertyMetadata(96d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Largo del rayo al máximo. El mínimo es una fracción fija de este valor.</summary>
    public static readonly DependencyProperty MaximumBarLengthProperty =
        DependencyProperty.Register(
            nameof(MaximumBarLength),
            typeof(double),
            typeof(CoverVisualizer),
            new FrameworkPropertyMetadata(22d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BarThicknessProperty =
        DependencyProperty.Register(
            nameof(BarThickness),
            typeof(double),
            typeof(CoverVisualizer),
            new FrameworkPropertyMetadata(
                3d,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnPenChanged));

    public Brush BarBrush
    {
        get => (Brush)GetValue(BarBrushProperty);
        set => SetValue(BarBrushProperty, value);
    }

    public double InnerRadius
    {
        get => (double)GetValue(InnerRadiusProperty);
        set => SetValue(InnerRadiusProperty, value);
    }

    public double MaximumBarLength
    {
        get => (double)GetValue(MaximumBarLengthProperty);
        set => SetValue(MaximumBarLengthProperty, value);
    }

    public double BarThickness
    {
        get => (double)GetValue(BarThicknessProperty);
        set => SetValue(BarThicknessProperty, value);
    }

    private static void OnPenChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) =>
        ((CoverVisualizer)sender)._pen = null;

    /// <summary>
    /// Entrega los niveles del fotograma. Se copian y no se guarda la lista de quien llama: la
    /// produce un hilo de audio que la reutiliza, y quedarse con la referencia significaría dibujar
    /// mientras alguien la reescribe.
    /// </summary>
    public void SetLevels(IReadOnlyList<double> levels)
    {
        ArgumentNullException.ThrowIfNull(levels);

        if (_levels.Length != levels.Count)
        {
            _levels = new double[levels.Count];
        }

        for (var i = 0; i < levels.Count; i++)
        {
            _levels[i] = levels[i];
        }

        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        base.OnRender(drawingContext);

        if (_levels.Length == 0 || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        _pen ??= CreatePen();

        var centre = new Point(ActualWidth / 2, ActualHeight / 2);
        var inner = InnerRadius;
        var maximum = MaximumBarLength;

        // El largo en reposo tiene que sobresalir de la carátula, no esconderse debajo. Con un 18%
        // el rayo quieto medía cinco píxeles y la portada le tapaba cuatro: quedaba un píxel
        // asomando y el marco parecía no existir hasta que sonaba un golpe fuerte.
        var minimum = maximum * 0.38;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            // Un rayo por posición del anillo, y el doble de rayos que de bandas: cada banda se
            // dibuja dos veces, una en cada mitad.
            var rays = _levels.Length * 2;

            for (var i = 0; i < rays; i++)
            {
                // Se arranca a las doce y se gira en el sentido del reloj, igual que los medidores
                // circulares del resto de Kohana. Que dos piezas distintas empiecen en sitios
                // distintos es de esas cosas que nadie sabe nombrar pero se sienten desordenadas.
                var angle = (2 * Math.PI * i / rays) - (Math.PI / 2);
                var cos = Math.Cos(angle);
                var sin = Math.Sin(angle);

                var level = _levels[AudioSpectrum.MirroredBand(i, rays, _levels.Length)];
                var length = minimum + ((maximum - minimum) * Math.Clamp(level, 0, 1));

                context.BeginFigure(
                    new Point(centre.X + (cos * inner), centre.Y + (sin * inner)),
                    isFilled: false,
                    isClosed: false);
                context.LineTo(
                    new Point(centre.X + (cos * (inner + length)), centre.Y + (sin * (inner + length))),
                    isStroked: true,
                    isSmoothJoin: false);
            }
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(null, _pen, geometry);
    }

    private Pen CreatePen()
    {
        var pen = new Pen(BarBrush, BarThickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };

        pen.Freeze();
        return pen;
    }
}
