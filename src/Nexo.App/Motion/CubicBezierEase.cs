using System.Windows;
using System.Windows.Media.Animation;

namespace Nexo.App.Motion;

/// <summary>
/// Diseño D26 — una curva de aceleración cúbica de Bézier arbitraria, la misma primitiva que usan
/// CSS y los sistemas de movimiento de escritorio modernos.
///
/// WPF trae curvas con nombre (<see cref="CubicEase"/>, <see cref="BackEase"/>…) pero ninguna deja
/// escribir los puntos de control, y las curvas que dan carácter a este rediseño —la "enfatizada"
/// que arranca lento y aterriza sin rebote, y el resorte que se pasa un poco y vuelve— no se pueden
/// aproximar con las que hay: <see cref="BackEase"/> se pasa por los dos extremos y
/// <see cref="ExponentialEase"/> nunca se pasa. La diferencia entre "se mueve" y "se siente vivo"
/// está justo en esos puntos de control.
///
/// <see cref="Y1"/> e <see cref="Y2"/> pueden salirse de [0,1] a propósito: ahí vive el rebote.
/// <see cref="X1"/> y <see cref="X2"/> no, porque una curva que retrocede en el tiempo no es una
/// función y el solucionador no tendría una única respuesta.
///
/// Implementa <see cref="IEasingFunction"/> sobre <see cref="Freezable"/> en vez de heredar de
/// <see cref="EasingFunctionBase"/>, y no por gusto: esa clase base aplica su propiedad
/// <c>EasingMode</c> —cuyo valor por omisión es <c>EaseOut</c>— sobre el resultado, lo que reflejaba
/// estas curvas y convertía la de entrada en la de salida. Los puntos de control ya describen la
/// curva entera; un modo encima solo puede estropearla, así que aquí esa propiedad no existe.
/// </summary>
public sealed class CubicBezierEase : Freezable, IEasingFunction
{
    private const int NewtonIterations = 8;
    private const double NewtonMinimumSlope = 0.001;
    private const double SubdivisionPrecision = 0.0000001;
    private const int SubdivisionMaximumIterations = 12;

    public static readonly DependencyProperty X1Property = DependencyProperty.Register(
        nameof(X1), typeof(double), typeof(CubicBezierEase), new PropertyMetadata(0.0));

    public static readonly DependencyProperty Y1Property = DependencyProperty.Register(
        nameof(Y1), typeof(double), typeof(CubicBezierEase), new PropertyMetadata(0.0));

    public static readonly DependencyProperty X2Property = DependencyProperty.Register(
        nameof(X2), typeof(double), typeof(CubicBezierEase), new PropertyMetadata(1.0));

    public static readonly DependencyProperty Y2Property = DependencyProperty.Register(
        nameof(Y2), typeof(double), typeof(CubicBezierEase), new PropertyMetadata(1.0));

    public double X1
    {
        get => (double)GetValue(X1Property);
        set => SetValue(X1Property, value);
    }

    public double Y1
    {
        get => (double)GetValue(Y1Property);
        set => SetValue(Y1Property, value);
    }

    public double X2
    {
        get => (double)GetValue(X2Property);
        set => SetValue(X2Property, value);
    }

    public double Y2
    {
        get => (double)GetValue(Y2Property);
        set => SetValue(Y2Property, value);
    }

    protected override Freezable CreateInstanceCore() => new CubicBezierEase();

    public double Ease(double normalizedTime)
    {
        if (normalizedTime <= 0)
        {
            return 0;
        }

        if (normalizedTime >= 1)
        {
            return 1;
        }

        var x1 = Math.Clamp(X1, 0, 1);
        var x2 = Math.Clamp(X2, 0, 1);

        return SampleCurve(SolveForT(normalizedTime, x1, x2), Y1, Y2);
    }

    private static double SampleCurve(double t, double a1, double a2)
    {
        // Forma polinómica de una Bézier cúbica con extremos fijos en 0 y 1.
        var c = 3 * a1;
        var b = 3 * (a2 - a1) - c;
        var a = 1 - c - b;
        return ((a * t + b) * t + c) * t;
    }

    private static double SampleCurveDerivative(double t, double a1, double a2)
    {
        var c = 3 * a1;
        var b = 3 * (a2 - a1) - c;
        var a = 1 - c - b;
        return (3 * a * t + 2 * b) * t + c;
    }

    /// <summary>
    /// Newton-Raphson mientras la pendiente sea utilizable; bisección cuando no lo es. Newton solo
    /// converge donde la curva sube de verdad, y estas curvas tienen tramos casi planos a propósito
    /// —justo los que dan la sensación de arranque lento—, así que hace falta la red de abajo.
    /// </summary>
    private static double SolveForT(double x, double x1, double x2)
    {
        var t = x;

        for (var iteration = 0; iteration < NewtonIterations; iteration++)
        {
            var slope = SampleCurveDerivative(t, x1, x2);
            if (Math.Abs(slope) < NewtonMinimumSlope)
            {
                break;
            }

            var error = SampleCurve(t, x1, x2) - x;
            t -= error / slope;
        }

        if (t is >= 0 and <= 1 &&
            Math.Abs(SampleCurve(t, x1, x2) - x) < SubdivisionPrecision)
        {
            return t;
        }

        var low = 0.0;
        var high = 1.0;
        t = x;

        for (var iteration = 0; iteration < SubdivisionMaximumIterations; iteration++)
        {
            var current = SampleCurve(t, x1, x2);
            if (Math.Abs(current - x) < SubdivisionPrecision)
            {
                return t;
            }

            if (x > current)
            {
                low = t;
            }
            else
            {
                high = t;
            }

            t = (high - low) / 2 + low;
        }

        return t;
    }
}
