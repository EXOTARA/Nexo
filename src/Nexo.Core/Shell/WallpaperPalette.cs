namespace Nexo.Core.Shell;

/// <summary>Un color dominante del fondo, con la proporción de imagen que ocupa (0 a 1).</summary>
public readonly record struct PaletteColor(RgbColor Color, double Share);

/// <summary>
/// Diseño D31 — reduce los millones de píxeles de un fondo de escritorio a los pocos colores que de
/// verdad lo definen.
///
/// El método es un histograma tridimensional: se recorta la precisión de cada canal a 5 bits, se
/// cuentan los píxeles que caen en cada cubo y se devuelve el promedio real de cada cubo, no su
/// centro. Recortar a 5 bits es lo que hace que un degradado —donde casi ningún píxel repite color
/// exacto— se agrupe en unas pocas familias en vez de en miles de tonos únicos; devolver el
/// promedio y no el centro del cubo es lo que evita que el color salga desplazado hacia una rejilla
/// artificial.
///
/// Cuál de esos colores sirve como acento es una decisión aparte, en <see cref="AccentPicker"/>: el
/// más frecuente de una foto suele ser un gris o un negro, y eso no es un acento.
/// </summary>
public static class WallpaperPalette
{
    /// <summary>Bits que se conservan por canal. Cinco da 32 niveles y 32 768 cubos posibles.</summary>
    private const int BitsPerChannel = 5;

    private const int Shift = 8 - BitsPerChannel;

    /// <summary>
    /// Cuántos colores se devuelven. Fueron 8 y se quedaba corto de una forma que solo se ve con un
    /// fondo real: en una imagen dominada por un neutro —un fondo casi todo blanco con unas alas
    /// carmesí— los ocho cubos más frecuentes eran los ocho tonos de blanco, y el carmesí, que es lo
    /// que cualquiera diría que define esa imagen, se quedaba fuera antes de que
    /// <see cref="AccentPicker"/> llegara a verlo. Ordenar por frecuencia y cortar pronto descarta
    /// justo el color que da carácter.
    ///
    /// Se amplía en vez de cambiar el orden porque el selector ya pondera croma contra proporción:
    /// dándole más candidatos hace su trabajo, y un cubo grande pero gris sigue perdiendo contra uno
    /// pequeño y vivo.
    /// </summary>
    public const int DefaultMaximumColors = 48;

    public static IReadOnlyList<PaletteColor> Quantize(
        IReadOnlyList<RgbColor> pixels,
        int maximumColors = DefaultMaximumColors)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumColors);

        if (pixels.Count == 0)
        {
            return [];
        }

        var buckets = new Dictionary<int, Bucket>();

        foreach (var pixel in pixels)
        {
            var key = ((pixel.R >> Shift) << (BitsPerChannel * 2)) |
                      ((pixel.G >> Shift) << BitsPerChannel) |
                      (pixel.B >> Shift);

            buckets.TryGetValue(key, out var bucket);
            buckets[key] = bucket.Add(pixel);
        }

        return buckets.Values
            .OrderByDescending(bucket => bucket.Count)
            .Take(maximumColors)
            .Select(bucket => new PaletteColor(bucket.Average(), (double)bucket.Count / pixels.Count))
            .ToArray();
    }

    private readonly record struct Bucket(long SumR, long SumG, long SumB, int Count)
    {
        public Bucket Add(RgbColor pixel) =>
            new(SumR + pixel.R, SumG + pixel.G, SumB + pixel.B, Count + 1);

        public RgbColor Average() => new(
            (byte)(SumR / Count),
            (byte)(SumG / Count),
            (byte)(SumB / Count));
    }
}
