namespace Nexo.Core.Metrics;

/// <summary>Velocidad de red instantánea, en bytes por segundo.</summary>
public readonly record struct NetworkThroughput(double DownloadBytesPerSecond, double UploadBytesPerSecond);

/// <summary>
/// Diseño D42 — convierte los contadores acumulados de la tarjeta de red en una velocidad.
///
/// Windows no informa «vas a 5 MB/s»: informa cuántos bytes ha movido la tarjeta desde que arrancó.
/// La velocidad es la diferencia entre dos lecturas partida por el tiempo transcurrido, y ahí hay dos
/// casos que hay que tratar o el número sale absurdo:
///
/// El contador puede reiniciarse o dar un salto hacia atrás —al reconectar la red, al suspender el
/// equipo, al desactivar una interfaz—. Una diferencia negativa partida por el tiempo daría una
/// velocidad negativa, y presentada como «descarga» no significa nada.
///
/// El tiempo transcurrido puede ser cero o casi cero si dos lecturas caen juntas. Dividir por eso da
/// infinito, que en pantalla aparece como un número disparatado durante un instante.
/// </summary>
public static class NetworkRate
{
    /// <summary>
    /// Por debajo de esto no se calcula nada: dos lecturas tan seguidas no dicen nada útil sobre la
    /// velocidad y el resultado tiende a infinito.
    /// </summary>
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromMilliseconds(100);

    public static NetworkThroughput Compute(
        long downloadedBytesNow,
        long uploadedBytesNow,
        long downloadedBytesBefore,
        long uploadedBytesBefore,
        TimeSpan elapsed)
    {
        if (elapsed < MinimumInterval)
        {
            return new NetworkThroughput(0, 0);
        }

        var seconds = elapsed.TotalSeconds;

        return new NetworkThroughput(
            RatePerSecond(downloadedBytesNow, downloadedBytesBefore, seconds),
            RatePerSecond(uploadedBytesNow, uploadedBytesBefore, seconds));
    }

    private static double RatePerSecond(long now, long before, double seconds)
    {
        var delta = now - before;

        // Un contador que retrocede significa que se reinició, no que se haya movido nada hacia
        // atrás. Se informa cero hasta la siguiente lectura, que ya tendrá una base coherente.
        return delta <= 0 ? 0 : delta / seconds;
    }
}
