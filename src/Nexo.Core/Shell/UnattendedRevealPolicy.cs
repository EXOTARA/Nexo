namespace Nexo.Core.Shell;

/// <summary>
/// Diseño D58 — cuándo se retira solo algo que se abrió solo.
///
/// El gesto del borde abre el shell sin que nadie lo pida explícitamente: basta con quedarse un
/// momento en la franja, y eso pasa sin querer al ir a por la barra de desplazamiento o al cruzar
/// entre dos monitores. Abrirse por roce es aceptable **si también sabe irse por sí mismo**; si no,
/// cada roce deja una ventana encima del trabajo que hay que cerrar a mano, y eso es exactamente lo
/// que se siente como invasivo.
///
/// La regla no es «no hay ratón encima» sino «no hay ratón encima **y** no ha pasado nada». Ese
/// «nada» es la parte que importa: leer una respuesta larga sin tocar el ratón no es abandono, y
/// una ventana que se cierra mientras la lees es peor que una que se queda. Por eso cuenta como
/// actividad cualquier señal de que alguien está ahí —teclear, escribir, tener el foco— y no solo
/// el puntero.
///
/// Solo se aplica a lo que se abrió por roce. Lo que se abre a propósito —el atajo, el icono de la
/// bandeja— se queda hasta que se cierre a propósito: retirar algo que se acaba de pedir es
/// desobedecer.
/// </summary>
public static class UnattendedRevealPolicy
{
    /// <summary>
    /// Cuánto se espera desde que el puntero sale hasta retirarse.
    ///
    /// Es más largo que la gracia del cajón (650 ms) a propósito: el cajón es una consulta de un
    /// vistazo y el shell es donde se conversa, así que salirse un momento del shell —a por el
    /// teclado, a mirar otra ventana— tiene que caber sin perderlo. Pero sigue siendo corto: si
    /// pasan varios segundos sin puntero, sin teclas y sin foco, nadie lo está usando.
    /// </summary>
    public static readonly TimeSpan GraceAfterPointerLeaves = TimeSpan.FromSeconds(2.5);

    /// <summary>
    /// Si toca retirarse, dadas las señales de ahora.
    ///
    /// <paramref name="outsideSince"/> es desde cuándo el puntero está fuera, o <c>null</c> si está
    /// dentro. <paramref name="hasUserAttention"/> recoge el resto de señales de presencia: foco de
    /// teclado, texto a medio escribir, o cualquier cosa que diga que hay alguien delante.
    /// </summary>
    public static bool ShouldRetract(
        bool openedByHover,
        DateTimeOffset? outsideSince,
        DateTimeOffset now,
        bool hasUserAttention)
    {
        if (!openedByHover || hasUserAttention || outsideSince is not { } since)
        {
            return false;
        }

        // Un reloj que va hacia atrás no es una espera cumplida.
        var away = now - since;
        return away >= GraceAfterPointerLeaves;
    }
}
