namespace Nexo.Core.Media;

/// <summary>
/// Diseño D43 — el tiempo de una pista, en texto y en fracción.
///
/// Está aquí y no en la vista porque Windows entrega posiciones que no siempre tienen sentido: una
/// pista en directo informa duración cero, un vídeo de YouTube puede devolver una posición mayor
/// que la duración justo al cambiar de pista, y un reproductor que aún no ha arrancado informa una
/// posición negativa. Cada uno de esos casos rompe una barra de progreso de una manera distinta y
/// hay que poder probarlos sin abrir Spotify.
/// </summary>
public static class MediaProgress
{
    /// <summary>
    /// «0:15», «4:05», «1:02:03». Los minutos solo llevan cero delante cuando hay horas: «0:05» es
    /// como se lee un reproductor, «00:05» es como se lee un cronómetro.
    /// </summary>
    public static string Format(TimeSpan? time)
    {
        if (time is not { } value || value < TimeSpan.Zero)
        {
            return "0:00";
        }

        return value.TotalHours >= 1
            ? $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}"
            : $"{value.Minutes}:{value.Seconds:00}";
    }

    /// <summary>
    /// Cuánto se admite avanzar por cuenta propia sobre la última posición conocida.
    ///
    /// Es una red de seguridad, no un límite de funcionamiento: si el equipo se suspende, si el
    /// reproductor deja de informar o si alguien salta a otra pista, la última posición conocida se
    /// queda vieja de golpe. Sin tope, la barra seguiría avanzando sola durante minutos y contaría
    /// una mentira cada vez mayor; con él, se queda quieta hasta la siguiente lectura de verdad.
    /// </summary>
    public static readonly TimeSpan MaximumExtrapolation = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Diseño D48 — la posición real ahora mismo, contando desde la última que informó el
    /// reproductor.
    ///
    /// Windows **no** actualiza la posición de la pista continuamente: la refresca cuando al
    /// reproductor le parece, y Spotify lo hace cada cinco o seis segundos. Leerla tal cual da una
    /// barra que se queda clavada varios segundos y luego pega un salto — exactamente el retraso
    /// que se ve al mirarla. Por eso la sesión trae también el instante en que esa posición fue
    /// cierta: con esos dos datos y el reloj, la posición de ahora es una suma.
    ///
    /// Solo se cuenta el tiempo si está sonando. En pausa la posición no avanza, y extrapolarla
    /// haría que la barra siguiera corriendo con la música parada.
    /// </summary>
    public static TimeSpan? Resolve(
        TimeSpan? position,
        DateTimeOffset? lastUpdatedAt,
        DateTimeOffset now,
        bool isPlaying,
        TimeSpan? duration)
    {
        if (position is not { } known)
        {
            return null;
        }

        if (!isPlaying || lastUpdatedAt is not { } anchor)
        {
            return known;
        }

        var elapsed = now - anchor;

        // Un tiempo negativo significa que los dos relojes no coinciden, no que la pista haya
        // retrocedido. Restarlo dejaría la barra por detrás de donde ya estaba.
        if (elapsed <= TimeSpan.Zero || elapsed > MaximumExtrapolation)
        {
            return known;
        }

        var advanced = known + elapsed;

        return duration is { } total && total > TimeSpan.Zero && advanced > total
            ? total
            : advanced;
    }

    /// <summary>
    /// Diseño D57 — lo que informa el reproductor, pasado a posición conocida o desconocida.
    ///
    /// El cero es válido: es donde empieza toda pista. Lo que no es válido es un tiempo negativo,
    /// que es lo que informa un reproductor que aún no ha arrancado.
    ///
    /// La distinción estaba mal hecha y se veía en cada canción. Al empezar una pista el
    /// reproductor informa 0 durante los primeros segundos; ese 0 se tomaba por «no se sabe», y sin
    /// posición conocida no hay desde dónde contar el tiempo. El cronómetro se quedaba clavado en
    /// «0:00» y la barra a cero hasta que el reproductor informaba su primera posición de verdad
    /// —Spotify tarda cinco o seis segundos— y entonces pegaba el salto. Es el mismo síntoma que
    /// <see cref="Resolve"/> arregló para el resto de la pista, intacto al principio de todas.
    /// </summary>
    public static TimeSpan? ReadPosition(TimeSpan? reported) =>
        reported is { } span && span >= TimeSpan.Zero ? span : null;

    /// <summary>
    /// Lo mismo para la duración, con la regla contraria en el cero: aquí cero significa «esto no
    /// tiene final» —una emisión en directo— y no «dura nada».
    /// </summary>
    public static TimeSpan? ReadDuration(TimeSpan? reported) =>
        reported is { } span && span > TimeSpan.Zero ? span : null;

    /// <summary>
    /// La fracción recorrida entre 0 y 1, o <c>null</c> cuando no se puede saber. Null y cero son
    /// cosas distintas: una radio en directo no está al principio de nada.
    /// </summary>
    public static double? Fraction(TimeSpan? position, TimeSpan? duration)
    {
        if (duration is not { } total || total <= TimeSpan.Zero)
        {
            return null;
        }

        if (position is not { } current || current < TimeSpan.Zero)
        {
            return 0;
        }

        return current >= total ? 1 : current.TotalSeconds / total.TotalSeconds;
    }
}
