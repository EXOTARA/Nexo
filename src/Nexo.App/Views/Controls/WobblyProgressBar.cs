using System.Windows;
using System.Windows.Media;

namespace Nexo.App.Views.Controls;

/// <summary>
/// Diseño D46 — la barra de reproducción ondulada de la referencia: lo ya escuchado es una onda que
/// avanza, lo que queda es una línea recta.
///
/// El contraste entre las dos mitades es lo que hace legible el gesto. Si ondulara entera, la
/// posición dejaría de leerse de un vistazo, que es lo único que una barra de reproducción tiene
/// que hacer bien; y si la onda no avanzara, sería un adorno fijo en vez de una señal de que algo
/// está sonando.
///
/// La onda se aplana al acercarse al cabezal y en pausa. Al final de la parte recorrida hay un
/// punto donde la amplitud tiene que llegar a cero: sin eso, la onda choca de golpe con la línea
/// recta y se ve un escalón.
/// </summary>
public sealed class WobblyProgressBar : FrameworkElement
{
    private const double WaveLength = 26;
    private const double Amplitude = 3.2;
    private const int SamplesPerWave = 8;

    private Pen? _playedPen;
    private Pen? _remainingPen;

    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(
            nameof(Progress),
            typeof(double),
            typeof(WobblyProgressBar),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Desplazamiento de la onda, en píxeles. Lo mueve quien dibuja el fotograma; el control no
    /// lleva reloj propio para que una sola cadencia gobierne toda la pestaña.
    /// </summary>
    public static readonly DependencyProperty PhaseProperty =
        DependencyProperty.Register(
            nameof(Phase),
            typeof(double),
            typeof(WobblyProgressBar),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>En pausa la onda se aplana: la barra sigue ahí, pero deja de estar viva.</summary>
    public static readonly DependencyProperty IsWavingProperty =
        DependencyProperty.Register(
            nameof(IsWaving),
            typeof(bool),
            typeof(WobblyProgressBar),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PlayedBrushProperty =
        DependencyProperty.Register(
            nameof(PlayedBrush),
            typeof(Brush),
            typeof(WobblyProgressBar),
            new FrameworkPropertyMetadata(
                Brushes.White,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnPenChanged));

    public static readonly DependencyProperty RemainingBrushProperty =
        DependencyProperty.Register(
            nameof(RemainingBrush),
            typeof(Brush),
            typeof(WobblyProgressBar),
            new FrameworkPropertyMetadata(
                Brushes.Gray,
                FrameworkPropertyMetadataOptions.AffectsRender,
                OnPenChanged));

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public double Phase
    {
        get => (double)GetValue(PhaseProperty);
        set => SetValue(PhaseProperty, value);
    }

    public bool IsWaving
    {
        get => (bool)GetValue(IsWavingProperty);
        set => SetValue(IsWavingProperty, value);
    }

    public Brush PlayedBrush
    {
        get => (Brush)GetValue(PlayedBrushProperty);
        set => SetValue(PlayedBrushProperty, value);
    }

    public Brush RemainingBrush
    {
        get => (Brush)GetValue(RemainingBrushProperty);
        set => SetValue(RemainingBrushProperty, value);
    }

    private static void OnPenChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var bar = (WobblyProgressBar)sender;
        bar._playedPen = null;
        bar._remainingPen = null;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        base.OnRender(drawingContext);

        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        _playedPen ??= CreatePen(PlayedBrush, 3.4);
        _remainingPen ??= CreatePen(RemainingBrush, 3.4);

        var middle = height / 2;
        var head = Math.Clamp(Progress, 0, 1) * width;
        var amplitude = IsWaving ? Amplitude : 0;

        if (head > 0.5)
        {
            var wave = new StreamGeometry();
            using (var context = wave.Open())
            {
                context.BeginFigure(new Point(0, middle), isFilled: false, isClosed: false);

                var step = WaveLength / SamplesPerWave;
                for (var x = step; x <= head; x += step)
                {
                    // La amplitud cae en los últimos píxeles para que la onda entre en el cabezal
                    // sin escalón.
                    var fade = Math.Min(1, (head - x) / (WaveLength / 2));
                    var y = middle + (Math.Sin(((x + Phase) / WaveLength) * 2 * Math.PI) * amplitude * fade);
                    context.LineTo(new Point(x, y), isStroked: true, isSmoothJoin: true);
                }

                context.LineTo(new Point(head, middle), isStroked: true, isSmoothJoin: true);
            }

            wave.Freeze();
            drawingContext.DrawGeometry(null, _playedPen, wave);
        }

        if (head < width - 0.5)
        {
            drawingContext.DrawLine(_remainingPen, new Point(head, middle), new Point(width, middle));
        }
    }

    private static Pen CreatePen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };

        pen.Freeze();
        return pen;
    }
}
