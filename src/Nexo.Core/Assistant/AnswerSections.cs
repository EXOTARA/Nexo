using System.Text;

namespace Nexo.Core.Assistant;

/// <summary>Un bloque con nombre dentro de una respuesta. El título va vacío en la entradilla.</summary>
public sealed record AnswerSection(string Title, string Body);

/// <summary>
/// Diseño D58 — parte una respuesta larga en los bloques que ya trae dentro.
///
/// Los modelos ya estructuran: escriben «Ingredientes», «Paso a paso», «Notas». Lo que fallaba era
/// el dibujo — todo llegaba como un solo párrafo continuo con asteriscos crudos alrededor de los
/// títulos, así que la estructura estaba en el texto pero no en la pantalla, y una receta se leía
/// como un muro donde hay que buscar dónde acaban los ingredientes y empiezan los pasos.
///
/// Aquí solo se reconoce; el dibujo es de la vista. Está en Core porque lo difícil no es pintar una
/// caja: es decidir qué línea es un título, y eso hay que poder probarlo sin abrir la aplicación.
///
/// **Se exige que haya de verdad estructura.** Con menos de dos bloques no se devuelve nada, y la
/// vista sigue enseñando la burbuja de siempre: meter una respuesta de dos frases en una caja con
/// título la hace parecer más importante y más larga de lo que es, y ese es el fallo contrario.
/// </summary>
public static class AnswerSections
{
    /// <summary>Mínimo de bloques con título para que valga la pena separarlos.</summary>
    private const int MinimumSections = 2;

    public static IReadOnlyList<AnswerSection> Parse(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return [];
        }

        var sections = new List<AnswerSection>();
        var title = string.Empty;
        var body = new StringBuilder();
        var titled = 0;

        foreach (var raw in answer.Replace("\r\n", "\n").Split('\n'))
        {
            if (TryReadHeading(raw, out var heading))
            {
                Flush(sections, title, body);
                title = heading;
                titled++;
                continue;
            }

            body.AppendLine(raw);
        }

        Flush(sections, title, body);

        // La entradilla no cuenta como bloque: una frase de presentación seguida de un único
        // apartado no es una respuesta estructurada, es un párrafo con un encabezado.
        return titled >= MinimumSections ? sections : [];
    }

    private static void Flush(List<AnswerSection> sections, string title, StringBuilder body)
    {
        var text = body.ToString().Trim();
        body.Clear();

        // Un título sin nada debajo se conserva igualmente: si el modelo lo escribió, quitarlo
        // dejaría un hueco inexplicable entre dos apartados.
        if (title.Length == 0 && text.Length == 0)
        {
            return;
        }

        sections.Add(new AnswerSection(title, text));
    }

    /// <summary>
    /// Reconoce las dos formas en que los modelos titulan: la de Markdown (<c>## Título</c>) y la
    /// línea entera en negrita (<c>**Título**</c>), que es la que más se ve y la que quedaba con los
    /// asteriscos a la vista.
    ///
    /// Se exige que el título ocupe la línea **entera**. Una negrita en mitad de una frase es
    /// énfasis, no un apartado, y tratarla como tal partiría la respuesta por donde no toca.
    /// </summary>
    private static bool TryReadHeading(string line, out string heading)
    {
        heading = string.Empty;
        var trimmed = line.Trim();

        if (trimmed.Length == 0)
        {
            return false;
        }

        if (trimmed[0] == '#')
        {
            var hashes = trimmed.TakeWhile(c => c == '#').Count();
            if (hashes is >= 1 and <= 6 && trimmed.Length > hashes && trimmed[hashes] == ' ')
            {
                heading = Clean(trimmed[hashes..]);
                return heading.Length > 0;
            }

            return false;
        }

        if (trimmed.Length >= 5 &&
            trimmed.StartsWith("**", StringComparison.Ordinal) &&
            trimmed.EndsWith("**", StringComparison.Ordinal))
        {
            var inner = trimmed[2..^2];

            // Dos negritas seguidas en la misma línea son dos énfasis, no un título.
            if (inner.Contains("**", StringComparison.Ordinal))
            {
                return false;
            }

            heading = Clean(inner);
            return heading.Length > 0;
        }

        return false;
    }

    /// <summary>Quita los dos puntos finales: «Ingredientes:» y «Ingredientes» son el mismo bloque.</summary>
    private static string Clean(string value) => value.Trim().TrimEnd(':').Trim();
}
