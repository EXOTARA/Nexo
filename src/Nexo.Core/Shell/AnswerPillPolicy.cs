namespace Nexo.Core.Shell;

/// <summary>
/// Diseño D34 — las reglas de la píldora que responde sin abrir Kohana.
///
/// Nace de una observación sencilla: quien pulsa Ctrl+Espacio no quiso abrir Kohana. Estaba en otra
/// aplicación y preguntó desde ahí. Traerle la ventana entera encima y dejarle la respuesta en la
/// pestaña de chat le obliga a volver a donde estaba: la respuesta llega, pero el gesto se
/// convierte en una interrupción.
///
/// Lo que decide esta clase es cuánto se queda la respuesta en pantalla. Un tiempo fijo no sirve:
/// «Sí, 15 de septiembre» y tres párrafos sobre la guerra de independencia no se leen en lo mismo.
/// Ocho segundos —lo que usaba la píldora anterior— es holgado para lo primero y deja lo segundo a
/// medias, y una respuesta que se va antes de terminar de leerla es peor que no haberla mostrado,
/// porque hay que volver a preguntar.
/// </summary>
public static class AnswerPillPolicy
{
    /// <summary>
    /// Palabras por minuto de lectura. 200 es lo habitual para prosa en pantalla; se usa algo por
    /// debajo porque aquí se lee de reojo, sin haber dejado la tarea que se estaba haciendo.
    /// </summary>
    public const int WordsPerMinute = 180;

    /// <summary>
    /// Nadie lee nada en menos de esto, ni siquiera una palabra: hay que darse cuenta de que
    /// apareció algo, mirarlo y volver.
    /// </summary>
    public static readonly TimeSpan MinimumVisible = TimeSpan.FromSeconds(6);

    /// <summary>
    /// Tope. Pasado este punto la respuesta ya no es «un vistazo» y lo razonable es abrirla en
    /// Kohana, no dejar una ventana flotando encima de lo que la persona esté haciendo.
    /// </summary>
    public static readonly TimeSpan MaximumVisible = TimeSpan.FromSeconds(40);

    /// <summary>
    /// A partir de aquí la respuesta ya no cabe cómodamente en una píldora de esquina y se ofrece
    /// abrirla en Kohana. No se recorta el texto: se muestra lo que quepa y se deja la puerta.
    /// </summary>
    public const int LongAnswerCharacters = 600;

    public static TimeSpan ReadingTimeFor(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return MinimumVisible;
        }

        var words = CountWords(text);
        var seconds = words / (double)WordsPerMinute * 60;
        var reading = TimeSpan.FromSeconds(seconds);

        return reading < MinimumVisible
            ? MinimumVisible
            : reading > MaximumVisible
                ? MaximumVisible
                : reading;
    }

    public static bool DeservesOpeningInKohana(string? text) =>
        !string.IsNullOrWhiteSpace(text) && text.Length > LongAnswerCharacters;

    private static int CountWords(string text)
    {
        var words = 0;
        var insideWord = false;

        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                insideWord = false;
                continue;
            }

            if (!insideWord)
            {
                insideWord = true;
                words++;
            }
        }

        return words;
    }
}
