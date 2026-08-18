namespace Nexo.Core.Shell;

/// <summary>Un punto del contorno, en coordenadas de dibujo.</summary>
public readonly record struct PetalPoint(double X, double Y);

/// <summary>
/// Diseño D49 — el contorno de flor con el que se recorta la carátula.
///
/// En la referencia el disco no es un círculo limpio: tiene el borde ondulado, y de ahí salen los
/// rayos. Kohana significa «florecilla» y la marca ya es sakura, así que la forma no es un adorno
/// importado — es la marca apareciendo donde toca.
///
/// Se describe en coordenadas polares, <c>r(θ) = radio · (1 + profundidad · cos(pétalos · θ))</c>,
/// y no como una lista de curvas dibujadas a mano. La diferencia importa por dos razones:
///
/// La misma forma se usa a 188 píxeles en la pestaña Media y a 46 en el resumen del Panel. Un
/// contorno fijo copiado de un archivo de diseño se escala mal a la segunda medida: lo que a tamaño
/// grande es una ondulación elegante, reducido a la cuarta parte se convierte en una mancha con
/// bordes sucios. Con una fórmula, la profundidad de los pétalos se ajusta por tamaño.
///
/// Y es comprobable. Que un contorno cerrado no tenga picos, no se salga de su caja y dé la vuelta
/// entera son cosas que se afirman con una prueba, no mirando la pantalla y diciendo «se ve bien».
/// </summary>
public static class PetalShape
{
    /// <summary>
    /// Profundidad por omisión. Es deliberadamente pequeña: por encima de ~0.12 la forma deja de
    /// leerse como un disco con el borde vivo y pasa a leerse como una estrella, y una carátula con
    /// forma de estrella tapa la imagen en vez de enmarcarla.
    /// </summary>
    public const double DefaultDepth = 0.055;

    public const int DefaultPetals = 8;

    /// <summary>
    /// El contorno como puntos, ya cerrado (el último punto NO repite el primero; eso lo decide
    /// quien dibuje).
    /// </summary>
    /// <param name="radius">Radio medio, la mitad del lado de la caja.</param>
    /// <param name="petals">Número de lóbulos.</param>
    /// <param name="depth">Cuánto se separa del círculo, como fracción del radio.</param>
    /// <param name="samplesPerPetal">
    /// Muestras por lóbulo. Con menos de ocho la curva se ve poligonal justo en las crestas, que es
    /// donde más se nota.
    /// </param>
    public static IReadOnlyList<PetalPoint> Describe(
        double radius,
        int petals = DefaultPetals,
        double depth = DefaultDepth,
        int samplesPerPetal = 16)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        ArgumentOutOfRangeException.ThrowIfLessThan(petals, 3);
        ArgumentOutOfRangeException.ThrowIfLessThan(samplesPerPetal, 4);

        if (depth < 0 || depth >= 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(depth),
                depth,
                "La profundidad va de 0 (círculo) a menos de 1; con 1 o más el contorno se cruza consigo mismo.");
        }

        var count = petals * samplesPerPetal;
        var points = new PetalPoint[count];

        for (var i = 0; i < count; i++)
        {
            // Se arranca a las doce, igual que los medidores circulares del resto de Kohana: con
            // un número par de pétalos, empezar a las tres dejaría un valle arriba y la forma se
            // vería torcida junto a un anillo que sí empieza arriba.
            var angle = (2 * Math.PI * i / count) - (Math.PI / 2);
            var r = radius * (1 + (depth * Math.Cos(petals * (angle + (Math.PI / 2)))));

            points[i] = new PetalPoint(
                radius + (r * Math.Cos(angle)),
                radius + (r * Math.Sin(angle)));
        }

        return points;
    }

    /// <summary>
    /// La profundidad que le toca a un contorno de este tamaño.
    ///
    /// Una ondulación que a 188 píxeles mide diez y se ve como un borde vivo, a 46 mide dos y medio
    /// y solo consigue ensuciar el canto. Por debajo de cien píxeles la forma se va acercando al
    /// círculo, y a partir de cuarenta ya es un círculo: no hay sitio para una onda que se note sin
    /// deformar la imagen.
    /// </summary>
    public static double DepthForSize(double size)
    {
        if (size <= 48)
        {
            return 0;
        }

        if (size >= 160)
        {
            return DefaultDepth;
        }

        return DefaultDepth * (size - 48) / (160 - 48);
    }
}
