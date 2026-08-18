namespace Nexo.Core.Shell;

/// <summary>
/// Diseño D31 — la aritmética de color que necesita el tema derivado del fondo de escritorio.
///
/// Las fórmulas no son de cosecha propia: la luminancia relativa y la razón de contraste son las
/// que define WCAG 2.2, y es lo que permite afirmar «este acento se ve sobre este panel» en vez de
/// mirarlo y opinar. Importa porque el fondo lo pone la persona, no nosotros: puede ser una foto
/// casi negra, un blanco quemado o un gris muerto, y el tema tiene que seguir funcionando en los
/// tres casos sin que nadie lo revise a mano.
/// </summary>
public static class ColorMath
{
    /// <summary>
    /// Cuánto color tiene de verdad, de 0 (gris puro) a 1 (color al máximo). Es la distancia entre
    /// el canal más alto y el más bajo.
    ///
    /// Se usa esto y no la saturación HSL, y la diferencia no es académica: (252, 250, 255) es
    /// blanco a ojos de cualquiera, pero su saturación HSL vale 1.0 —el máximo— porque la fórmula
    /// normaliza contra un rango que ahí es diminuto. Al elegir acento, eso hacía que el blanco de
    /// un letrero ganara a un rosa neón. El croma del mismo color es 0.02, que es lo que se
    /// corresponde con lo que se ve.
    /// </summary>
    public static double Chroma(RgbColor color)
    {
        var r = color.R / 255d;
        var g = color.G / 255d;
        var b = color.B / 255d;
        return Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
    }

    /// <summary>
    /// El tono del color, de 0 a 360 grados. Un gris no tiene tono y devuelve 0, que es arbitrario
    /// pero irrelevante: quien lo pide siempre ha comprobado antes que hay croma suficiente.
    /// </summary>
    public static double Hue(RgbColor color)
    {
        var r = color.R / 255d;
        var g = color.G / 255d;
        var b = color.B / 255d;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var chroma = max - min;

        if (chroma <= 0)
        {
            return 0;
        }

        var hue = max == r
            ? ((g - b) / chroma) % 6
            : max == g
                ? ((b - r) / chroma) + 2
                : ((r - g) / chroma) + 4;

        hue *= 60;
        return hue < 0 ? hue + 360 : hue;
    }

    /// <summary>
    /// Construye un color a partir de tono, saturación y luminosidad.
    ///
    /// Diseño D33 — es lo que permite que las superficies compartan el tono del acento en vez de
    /// arrastrar el suyo propio. Antes se mezclaba un poco de acento sobre unos grises fijos, y esos
    /// grises no eran neutros: el de las tarjetas tenía el azul a más del doble que el rojo, así que
    /// por mucho que se le mezclara un acento naranja la tarjeta seguía viéndose azul mientras el
    /// resto del panel sí cambiaba. Partiendo del tono no queda nada que arrastrar.
    /// </summary>
    public static RgbColor FromHsl(double hue, double saturation, double lightness)
    {
        hue = ((hue % 360) + 360) % 360;
        saturation = Math.Clamp(saturation, 0, 1);
        lightness = Math.Clamp(lightness, 0, 1);

        var chroma = (1 - Math.Abs((2 * lightness) - 1)) * saturation;
        var second = chroma * (1 - Math.Abs(((hue / 60) % 2) - 1));
        var match = lightness - (chroma / 2);

        var (r, g, b) = hue switch
        {
            < 60 => (chroma, second, 0d),
            < 120 => (second, chroma, 0d),
            < 180 => (0d, chroma, second),
            < 240 => (0d, second, chroma),
            < 300 => (second, 0d, chroma),
            _ => (chroma, 0d, second)
        };

        return new RgbColor(
            ToByte(r + match),
            ToByte(g + match),
            ToByte(b + match));
    }

    private static byte ToByte(double value) =>
        (byte)Math.Clamp(Math.Round(value * 255, MidpointRounding.AwayFromZero), 0, 255);

    /// <summary>
    /// Luminancia relativa según WCAG 2.2. Pondera el verde muy por encima del azul porque el ojo
    /// humano lo percibe así, y es la que hay que usar para decidir si algo se distingue del fondo.
    /// </summary>
    public static double RelativeLuminance(RgbColor color) =>
        (0.2126 * Linearize(color.R)) +
        (0.7152 * Linearize(color.G)) +
        (0.0722 * Linearize(color.B));

    /// <summary>
    /// Razón de contraste WCAG, de 1 (idénticos) a 21 (negro contra blanco). El umbral de texto
    /// normal es 4.5 y el de texto grande y elementos gráficos, 3.
    /// </summary>
    public static double ContrastRatio(RgbColor first, RgbColor second)
    {
        var a = RelativeLuminance(first);
        var b = RelativeLuminance(second);
        var (lighter, darker) = a >= b ? (a, b) : (b, a);
        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>Mezcla lineal entre dos colores. 0 devuelve el primero, 1 el segundo.</summary>
    public static RgbColor Mix(RgbColor from, RgbColor to, double amount)
    {
        var clamped = Math.Clamp(amount, 0, 1);
        return new RgbColor(
            MixChannel(from.R, to.R, clamped),
            MixChannel(from.G, to.G, clamped),
            MixChannel(from.B, to.B, clamped));
    }

    private static byte MixChannel(byte from, byte to, double amount) =>
        (byte)Math.Round(from + ((to - from) * amount), MidpointRounding.AwayFromZero);

    private static double Linearize(byte channel)
    {
        var value = channel / 255d;
        return value <= 0.03928
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
