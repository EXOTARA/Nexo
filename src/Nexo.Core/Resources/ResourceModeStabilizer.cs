namespace Nexo.Core.Resources;

/// <summary>
/// Diseño D58 — exige que la carga se sostenga antes de creerse que el equipo está ocupado.
///
/// <see cref="ResourceGovernorPolicy"/> decide con la foto de un instante, y eso es correcto para
/// lo que hace: es una función pura y se puede probar. Lo que faltaba es quién decide si esa foto
/// merece cambiar el modo.
///
/// Sin esto, **una sola lectura** por encima del umbral bastaba para anunciar «El equipo está
/// ocupado» y apagar la IA local y Vision. Y los contadores de GPU de Windows son por proceso y por
/// motor: se suman los procesos de un mismo motor y se toma el más ocupado, así que un golpe de
/// decodificación de vídeo —un vídeo de YouTube, el propio escritorio componiendo— cruza el 88 %
/// durante una muestra sin que haya nada pesado abierto. Adler lo vio exactamente así: modo Ocupado
/// de la nada, sin haber pedido nada a la IA.
///
/// Se confirma en los dos sentidos. Solo al entrar dejaría el modo pegado: bastaría con una lectura
/// baja para salir, y una carga que oscila alrededor del umbral encendería y apagaría el aviso cada
/// pocos segundos, que molesta más que el fallo original.
///
/// **La pantalla completa no se retrasa.** Un juego que arranca tiene que silenciar los avisos ya:
/// esperar tres muestras es exactamente la ventana en la que un cajón se asoma encima de la
/// partida. Solo se confirma <see cref="ResourceMode.Busy"/>, que es el que se dispara por umbral.
/// </summary>
public sealed class ResourceModeStabilizer
{
    /// <summary>
    /// Lecturas seguidas que hacen falta para creerse un cambio. Con el muestreo del shell son unos
    /// pocos segundos: suficiente para descartar un pico, corto para no ir por detrás de la
    /// realidad cuando la carga es de verdad.
    /// </summary>
    public const int DefaultConfirmations = 3;

    private readonly int _confirmations;

    private ResourceGovernorDecision _applied = ResourceGovernorDecision.Normal;
    private bool _hasApplied;
    private int _agreeing;
    private bool _pendingIsBusy;

    public ResourceModeStabilizer(int confirmations = DefaultConfirmations)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(confirmations, 1);
        _confirmations = confirmations;
    }

    /// <summary>La decisión que hay que aplicar de verdad, dada la que acaba de calcularse.</summary>
    public ResourceGovernorDecision Stabilize(ResourceGovernorDecision candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (!_hasApplied)
        {
            _hasApplied = true;
            _applied = candidate;
            return _applied;
        }

        // El modo Juego no se debate: entrar y salir de pantalla completa es un hecho observado,
        // no una medida que pueda dar un pico.
        if (candidate.Mode == ResourceMode.Game || _applied.Mode == ResourceMode.Game)
        {
            _agreeing = 0;
            _applied = candidate;
            return _applied;
        }

        var candidateIsBusy = candidate.Mode == ResourceMode.Busy;
        var appliedIsBusy = _applied.Mode == ResourceMode.Busy;

        if (candidateIsBusy == appliedIsBusy)
        {
            // Coincide con lo que ya está puesto. Se refresca el texto —los porcentajes cambian
            // aunque el modo no— y se olvida cualquier cambio a medio confirmar.
            _agreeing = 0;
            _applied = candidate;
            return _applied;
        }

        if (_agreeing == 0 || _pendingIsBusy != candidateIsBusy)
        {
            _pendingIsBusy = candidateIsBusy;
            _agreeing = 1;
        }
        else
        {
            _agreeing++;
        }

        if (_agreeing < _confirmations)
        {
            return _applied;
        }

        _agreeing = 0;
        _applied = candidate;
        return _applied;
    }
}
