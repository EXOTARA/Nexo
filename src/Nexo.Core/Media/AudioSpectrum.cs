namespace Nexo.Core.Media;

/// <summary>
/// Diseño D46 — convierte el resultado crudo de una FFT en las barras del anillo que rodea la
/// carátula.
///
/// Vive aquí, separado de la captura de audio y del dibujo, porque es donde están todas las
/// decisiones que hacen que un visualizador se vea bien o se vea roto, y ninguna se puede juzgar
/// mirando una pantalla en movimiento:
///
/// **Las bandas son logarítmicas.** El oído lo es. Repartir el espectro en partes iguales mete casi
/// toda la música en las dos primeras barras y deja las demás quietas: el resultado parece un
/// medidor averiado. Cada banda cubre la misma distancia en octavas, no en hercios.
///
/// **El ataque es rápido y la caída lenta.** Sin esto el anillo tiembla, porque entre dos lecturas
/// consecutivas una banda puede pasar de todo a nada. Un golpe de batería tiene que aparecer al
/// instante —si se suaviza la subida, el visualizador va por detrás de la música y se nota— pero
/// bajar despacio, que es como se apaga un sonido de verdad.
///
/// **La referencia se adapta.** Una canción grabada bajita movería el anillo tres píxeles para
/// siempre. Se guarda el pico reciente y se mide contra él, así que cualquier música llena el
/// anillo; el pico también decae, o una explosión al principio de la canción dejaría todo lo demás
/// aplastado el resto del tema.
/// </summary>
public sealed class AudioSpectrum
{
    /// <summary>Frecuencia más baja que se representa. Por debajo hay más retumbe que música.</summary>
    public const double MinimumHertz = 40;

    /// <summary>Tope. Por encima queda el siseo, que aporta ruido visual y ninguna información.</summary>
    public const double MaximumHertz = 12000;

    private const double AttackPerFrame = 0.55;
    private const double DecayPerFrame = 0.16;
    private const double PeakDecayPerFrame = 0.006;
    private const double MinimumPeak = 0.0025;

    private readonly double[] _levels;
    private double _peak = MinimumPeak;

    public AudioSpectrum(int bandCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bandCount, 1);
        _levels = new double[bandCount];
    }

    public int BandCount => _levels.Length;

    /// <summary>Los niveles actuales, entre 0 y 1, ya suavizados.</summary>
    public IReadOnlyList<double> Levels => _levels;

    /// <summary>
    /// Reparte las magnitudes de la FFT en bandas y avanza el suavizado un fotograma.
    /// </summary>
    /// <param name="magnitudes">
    /// Magnitudes por bin, de 0 a Nyquist. Se espera solo la mitad útil de la FFT.
    /// </param>
    /// <param name="sampleRate">Muestras por segundo de la captura.</param>
    public void Push(IReadOnlyList<double> magnitudes, int sampleRate)
    {
        ArgumentNullException.ThrowIfNull(magnitudes);

        if (magnitudes.Count == 0 || sampleRate <= 0)
        {
            Decay();
            return;
        }

        var hertzPerBin = sampleRate / 2.0 / magnitudes.Count;
        var raw = new double[_levels.Length];
        var frameMaximum = 0.0;

        for (var band = 0; band < _levels.Length; band++)
        {
            var (low, high) = BandRange(band, _levels.Length);

            var from = (int)Math.Floor(low / hertzPerBin);
            var to = (int)Math.Ceiling(high / hertzPerBin);

            from = Math.Clamp(from, 0, magnitudes.Count - 1);
            to = Math.Clamp(to, from + 1, magnitudes.Count);

            // El máximo del tramo y no la media: promediar una banda ancha diluye el pico contra
            // sus vecinos silenciosos, y las barras agudas —que cubren muchos bins— no se moverían
            // nunca.
            var peak = 0.0;
            for (var bin = from; bin < to; bin++)
            {
                var value = Math.Abs(magnitudes[bin]);
                if (value > peak)
                {
                    peak = value;
                }
            }

            raw[band] = peak;
            if (peak > frameMaximum)
            {
                frameMaximum = peak;
            }
        }

        _peak = Math.Max(MinimumPeak, Math.Max(frameMaximum, _peak - (_peak * PeakDecayPerFrame)));

        for (var band = 0; band < _levels.Length; band++)
        {
            // Curva perceptiva. Dividir por el pico deja casi todas las bandas muy abajo, porque en
            // una mezcla real una sola banda se lleva la mayor parte de la energía y el resto vive
            // en la décima parte de eso. Sin corregirlo, el anillo apenas se mueve aunque la música
            // esté sonando fuerte: es exactamente lo que se veía. La raíz cúbica sube la zona media
            // sin tocar ni el silencio ni el máximo.
            var target = Math.Pow(Math.Clamp(raw[band] / _peak, 0, 1), 0.34);
            var current = _levels[band];

            _levels[band] = target > current
                ? current + ((target - current) * AttackPerFrame)
                : current + ((target - current) * DecayPerFrame);
        }
    }

    /// <summary>
    /// Baja las barras cuando no llega audio. Sin esto, pausar la música dejaría el anillo
    /// congelado en la última forma, que se lee como que sigue sonando.
    /// </summary>
    public void Decay()
    {
        for (var band = 0; band < _levels.Length; band++)
        {
            _levels[band] *= 1 - DecayPerFrame;
        }

        _peak = Math.Max(MinimumPeak, _peak - (_peak * PeakDecayPerFrame));
    }

    public void Reset()
    {
        Array.Clear(_levels);
        _peak = MinimumPeak;
    }

    /// <summary>
    /// Diseño D54 — qué banda le toca a cada rayo del anillo.
    ///
    /// Los rayos no se reparten las bandas en orden. Puestos en fila alrededor del círculo, el rayo
    /// cero lleva los graves y el último los agudos, y como la música vive casi entera en la mitad
    /// grave, solo se movía un lado del anillo — siempre el mismo. Era el defecto que se veía.
    ///
    /// Con el espejo, el círculo recorre el espectro de grave a agudo en la mitad derecha y vuelve
    /// de agudo a grave en la izquierda. Las dos mitades son simétricas, así que el anillo respira
    /// entero en vez de agitarse por un costado. Es como se construye cualquier visualizador
    /// radial, y no es solo estética: un anillo asimétrico se lee como estropeado.
    /// </summary>
    public static int MirroredBand(int ray, int rayCount, int bandCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ray);
        ArgumentOutOfRangeException.ThrowIfLessThan(rayCount, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(bandCount, 1);

        var half = rayCount / 2.0;
        var distance = ray <= half ? ray : rayCount - ray;
        var band = (int)Math.Round(distance / half * (bandCount - 1));

        return Math.Clamp(band, 0, bandCount - 1);
    }

    /// <summary>
    /// Los bordes en hercios de una banda, repartidos por octavas. Público porque es la decisión
    /// que hace que el visualizador se parezca a la música, y merece prueba propia.
    /// </summary>
    public static (double Low, double High) BandRange(int index, int bandCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfLessThan(bandCount, 1);

        var ratio = Math.Log(MaximumHertz / MinimumHertz);
        var low = MinimumHertz * Math.Exp(ratio * index / bandCount);
        var high = MinimumHertz * Math.Exp(ratio * (index + 1) / bandCount);
        return (low, high);
    }
}
