namespace Nexo.Core.Shell;

/// <summary>
/// Diseño D31 — decide qué color de la paleta del fondo sirve como acento de Sakura.
///
/// El color más frecuente de una foto casi nunca sirve: suele ser el negro de las sombras, el gris
/// del cielo nublado o el beige de una pared, y un acento así no se distingue del propio panel. Por
/// eso hay dos filtros y ninguno sobra:
///
/// El de croma descarta lo que no tiene color de verdad. Un gris —o un blanco casi puro— puede
/// contrastar perfectamente con el panel y aun así no ser un acento: el acento es lo que le da
/// color a la interfaz, no solo lo que se ve. Se mide con croma y no con saturación HSL por la
/// razón que explica <see cref="ColorMath.Chroma"/>: con saturación, el blanco de un letrero le
/// ganaba a un rosa neón.
///
/// El de contraste se mide de verdad contra el fondo del panel, con la fórmula de WCAG. La primera
/// versión de esto filtraba por claridad HSL, y era un sustituto pobre: un azul marino saturado
/// tiene claridad media y pasaba el filtro, pero su luminancia real es tan baja que sobre el panel
/// oscuro quedaba invisible. Una prueba con 300 paletas al azar lo destapó; a ojo no habría salido
/// hasta que a alguien le tocara ese fondo.
///
/// La ponderación usa la raíz de la proporción a propósito. Con la proporción cruda, un fondo con
/// un 70% de cielo azul pálido ganaría siempre a un 4% de rojo intenso; con la raíz, ocupar mucho
/// sigue contando pero deja de aplastar al color que de verdad da carácter a la imagen — que es lo
/// que una persona señalaría si le preguntaras «¿de qué color es este fondo?».
/// </summary>
public static class AccentPicker
{
    /// <summary>Por debajo de esto es un gris: técnicamente un color, pero no un acento.</summary>
    public const double MinimumChroma = 0.15;

    /// <summary>
    /// Proporción mínima de imagen para que un color cuente como acento. Acompaña a la ampliación
    /// de la paleta: con muchos más candidatos entran también cubos diminutos, y sin este suelo un
    /// puñado de píxeles —el borde con halo de una compresión JPEG sobre una foto en blanco y
    /// negro— podría convertirse en el color de toda la aplicación. Medio por ciento es poco para
    /// cualquier cosa que alguien llamaría «el color del fondo», y suficiente para descartar ruido.
    /// </summary>
    public const double MinimumShare = 0.005;

    /// <summary>
    /// Umbral de WCAG para elementos gráficos y texto grande. El acento se usa en iconos, bordes y
    /// botones, no en párrafos, así que 3:1 es el listón que corresponde.
    /// </summary>
    public const double MinimumContrast = 3.0;

    /// <summary>
    /// Devuelve el acento elegido, o <c>null</c> si ningún color del fondo sirve —porque todo es
    /// gris, o porque nada contrasta lo suficiente con el panel—. Devolver null y no un color
    /// inventado importa: quien llama debe poder conservar el tema anterior en vez de recibir un
    /// acento que no representa nada o que no se ve.
    /// </summary>
    public static RgbColor? Pick(IReadOnlyList<PaletteColor> palette, RgbColor surface)
    {
        ArgumentNullException.ThrowIfNull(palette);

        var candidate = palette
            .Where(entry => entry.Share >= MinimumShare)
            .Where(entry => ColorMath.Chroma(entry.Color) >= MinimumChroma)
            .OrderByDescending(Score)
            .Select(entry => (RgbColor?)entry.Color)
            .FirstOrDefault();

        return candidate is null ? null : Brighten(candidate.Value, surface);
    }

    /// <summary>
    /// Aclara el color hasta que se distinga de la superficie, conservando su tono.
    ///
    /// Existe porque rechazar por contraste era la respuesta equivocada, y se vio con un fondo real:
    /// una imagen oscura con cintas naranjas y magentas: sus colores tienen croma de sobra pero su
    /// contraste contra el panel se queda en 2.2, así que el filtro los tiraba todos y Sakura
    /// concluía que ese fondo "no tiene color". Cualquiera que lo mire diría que es naranja y
    /// morado. Los fondos oscuros son de lo más común, así que el caso no era raro: era el normal.
    ///
    /// Mezclar hacia el blanco conserva el tono y sube la luminancia, que es exactamente lo que
    /// hace falta. Se avanza en pasos pequeños y se para en cuanto cumple, para desteñir lo mínimo.
    /// </summary>
    private static RgbColor Brighten(RgbColor color, RgbColor surface)
    {
        const double step = 0.05;
        var white = new RgbColor(255, 255, 255);

        for (var amount = 0.0; amount <= 1.0; amount += step)
        {
            var candidate = ColorMath.Mix(color, white, amount);
            if (ColorMath.ContrastRatio(candidate, surface) >= MinimumContrast)
            {
                return candidate;
            }
        }

        // Ni el blanco puro contrasta con esta superficie: es una superficie casi blanca, y ahí el
        // acento tiene que oscurecerse en vez de aclararse.
        return Darken(color, surface);
    }

    private static RgbColor Darken(RgbColor color, RgbColor surface)
    {
        const double step = 0.05;
        var black = new RgbColor(0, 0, 0);

        for (var amount = 0.0; amount <= 1.0; amount += step)
        {
            var candidate = ColorMath.Mix(color, black, amount);
            if (ColorMath.ContrastRatio(candidate, surface) >= MinimumContrast)
            {
                return candidate;
            }
        }

        return color;
    }

    private static double Score(PaletteColor entry) =>
        Math.Sqrt(entry.Share) * ColorMath.Chroma(entry.Color);
}
