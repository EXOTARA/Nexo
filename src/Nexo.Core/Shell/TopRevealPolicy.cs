namespace Nexo.Core.Shell;

/// <summary>Lo que hay que saber del ratón y de la pantalla para decidir si el cajón debe bajar.</summary>
public sealed record TopRevealProbe(
    double CursorX,
    double CursorY,
    double WorkAreaLeft,
    double WorkAreaRight,
    double WorkAreaTop);

/// <summary>
/// Diseño D44 — llevar el ratón al centro del borde de arriba baja el cajón del panel.
///
/// Es el mismo gesto que <see cref="EdgeRevealPolicy"/> hace en los lados, con dos diferencias que
/// vienen del sitio: arriba no se puede usar todo el borde, porque ahí están la barra de título de
/// cualquier ventana maximizada y sus botones de cerrar. Por eso la zona es solo el tercio central,
/// que es donde una ventana maximizada no tiene nada con lo que competir.
///
/// La franja es más alta que la de los lados. En el borde de arriba no hay nada que detenga el
/// cursor —no es el final de la pantalla en un escritorio de varios monitores apilados— pero, sobre
/// todo, el gesto natural es empujar hacia arriba y frenar, no clavar el puntero en la primera
/// fila de píxeles.
/// </summary>
public static class TopRevealPolicy
{
    /// <summary>Alto de la franja sensible, desde el borde superior del área de trabajo.</summary>
    public const double HotZoneHeight = 20;

    /// <summary>
    /// Qué parte del ancho ocupa la zona, centrada. Un tercio es suficiente para alcanzarla sin
    /// apuntar y deja libres las dos esquinas de arriba, que son de Windows y de la ventana activa.
    /// </summary>
    public const double CenterWidthRatio = 1d / 3d;

    /// <summary>
    /// Cuánto hay que quedarse. Igual que en los lados: lo que distingue asomarse de pasar de
    /// largo no es la precisión, es quedarse.
    /// </summary>
    public static readonly TimeSpan Dwell = TimeSpan.FromMilliseconds(160);

    /// <summary>
    /// Tras cerrar el cajón el ratón suele seguir arriba, justo donde estaba. Sin esta pausa,
    /// cerrarlo volvería a abrirlo al instante.
    /// </summary>
    public static readonly TimeSpan CooldownAfterHide = TimeSpan.FromMilliseconds(900);

    public static bool IsInHotZone(TopRevealProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);

        var width = probe.WorkAreaRight - probe.WorkAreaLeft;
        if (width <= 0)
        {
            return false;
        }

        // El borde superior se incluye y el inferior de la franja no: [borde, borde + alto). Así
        // veinte píxeles son exactamente veinte, y el primero de fuera nunca cuenta como dentro.
        if (probe.CursorY < probe.WorkAreaTop || probe.CursorY >= probe.WorkAreaTop + HotZoneHeight)
        {
            return false;
        }

        var centerWidth = width * CenterWidthRatio;
        var left = probe.WorkAreaLeft + ((width - centerWidth) / 2);

        return probe.CursorX >= left && probe.CursorX <= left + centerWidth;
    }

    /// <summary>
    /// Ancho del cajón: la mitad larga de la pantalla, como en la referencia, con topes para que no
    /// quede ridículo en una pantalla muy ancha ni se salga en una pequeña.
    /// </summary>
    public static double ResolveWidth(double workAreaWidth)
    {
        if (workAreaWidth <= 0)
        {
            return MinimumWidth;
        }

        var width = workAreaWidth * 0.53;
        return Math.Clamp(width, MinimumWidth, MaximumWidth);
    }

    public const double MinimumWidth = 620;

    public const double MaximumWidth = 1180;
}
