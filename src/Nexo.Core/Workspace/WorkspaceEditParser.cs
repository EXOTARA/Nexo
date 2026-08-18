using System.Text;

namespace Nexo.Core.Workspace;

/// <summary>
/// Diseño D14 (Fase 5, nivel 4) — extrae un cambio propuesto de la respuesta del modelo.
///
/// Es una de esas piezas donde la tentación es "entender" lo que el modelo quiso decir. No se hace:
/// **el formato es exacto o no hay cambio**. Un parser tolerante que adivina rutas o recompone
/// bloques a medias acabaría escribiendo en el archivo equivocado, y ese error no lo ve nadie hasta
/// que ya ocurrió. Rechazar es barato; una respuesta que no cumple el formato simplemente se enseña
/// como texto y la persona no pierde nada.
///
/// Formato esperado, exactamente una vez en la respuesta:
///
/// <code>
/// KOHANA-EDIT: ruta/relativa/al/archivo.cs
/// KOHANA-WHY: una línea explicando el cambio
/// ```
/// contenido completo del archivo
/// ```
/// </code>
///
/// El contenido es el archivo ENTERO, no un parche. Aplicar parches exige resolver contexto y
/// ambigüedad; reemplazar el archivo completo es verificable releyendo, que es justo lo que el
/// coordinador hace.
/// </summary>
public static class WorkspaceEditParser
{
    public const string PathMarker = "KOHANA-EDIT:";

    public const string ReasonMarker = "KOHANA-WHY:";

    private const string Fence = "```";

    /// <summary>
    /// Instrucción que se le da al modelo. Vive aquí, junto al parser, para que el formato pedido y
    /// el formato aceptado no puedan separarse con el tiempo.
    /// </summary>
    public static string ModelInstructions =>
        "Si propones cambiar un archivo, responde EXACTAMENTE con este formato, una sola vez:" +
        Environment.NewLine + PathMarker + " ruta/relativa/del/archivo" +
        Environment.NewLine + ReasonMarker + " una línea explicando el cambio" +
        Environment.NewLine + Fence +
        Environment.NewLine + "el contenido COMPLETO del archivo, no un fragmento" +
        Environment.NewLine + Fence +
        Environment.NewLine + "Si no hace falta cambiar ningún archivo, responde normalmente y no uses ese formato.";

    /// <summary>
    /// Diseño D24 (nivel 5) — extrae TODOS los cambios propuestos, en orden. El nivel 4 sigue
    /// usando <see cref="Parse"/>, que exige exactamente uno; encadenar varios es el nivel 5 y por
    /// eso es un método distinto y no un parámetro: si fuera un parámetro, un descuido en la
    /// llamada convertiría un paso confirmado en cinco.
    ///
    /// El formato de cada bloque es el mismo, exacto, y un bloque mal formado **invalida la
    /// secuencia entera**. En una secuencia, saltarse el bloque que no se entiende es peor que en un
    /// cambio suelto: los pasos siguientes pueden dar por hecho el que se saltó.
    /// </summary>
    public static IReadOnlyList<WorkspaceEdit> ParseSequence(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return [];
        }

        var lines = answer.ReplaceLineEndings("\n").Split('\n');
        var edits = new List<WorkspaceEdit>();
        var index = 0;

        while (index < lines.Length)
        {
            if (!lines[index].TrimStart().StartsWith(PathMarker, StringComparison.Ordinal))
            {
                index++;
                continue;
            }

            var block = TryReadBlock(lines, index, out var next);
            if (block is null)
            {
                // Un bloque roto invalida la secuencia entera, a propósito.
                return [];
            }

            edits.Add(block);
            index = next;
        }

        return edits;
    }

    private static WorkspaceEdit? TryReadBlock(string[] lines, int pathIndex, out int nextIndex)
    {
        nextIndex = pathIndex + 1;

        var relativePath = lines[pathIndex].TrimStart()[PathMarker.Length..].Trim();
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        if (pathIndex + 1 >= lines.Length ||
            !lines[pathIndex + 1].TrimStart().StartsWith(ReasonMarker, StringComparison.Ordinal))
        {
            return null;
        }

        var reason = lines[pathIndex + 1].TrimStart()[ReasonMarker.Length..].Trim();

        var openFence = FindFence(lines, pathIndex + 2);
        if (openFence < 0)
        {
            return null;
        }

        var closeFence = FindFence(lines, openFence + 1);
        if (closeFence < 0)
        {
            return null;
        }

        var content = new StringBuilder();
        for (var line = openFence + 1; line < closeFence; line++)
        {
            content.Append(lines[line]).Append(Environment.NewLine);
        }

        nextIndex = closeFence + 1;

        var text = content.ToString();
        return string.IsNullOrWhiteSpace(text)
            ? null
            : new WorkspaceEdit(
                relativePath,
                text,
                string.IsNullOrWhiteSpace(reason) ? "Cambio propuesto por Kohana." : reason);
    }

    public static WorkspaceEdit? Parse(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return null;
        }

        var lines = answer.ReplaceLineEndings("\n").Split('\n');

        var pathIndex = IndexOfSingleMarker(lines, PathMarker);
        if (pathIndex < 0)
        {
            return null;
        }

        var relativePath = lines[pathIndex][PathMarker.Length..].Trim();
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var reasonIndex = IndexOfSingleMarker(lines, ReasonMarker);
        if (reasonIndex < 0 || reasonIndex < pathIndex)
        {
            return null;
        }

        var reason = lines[reasonIndex][ReasonMarker.Length..].Trim();

        var openFence = FindFence(lines, reasonIndex + 1);
        if (openFence < 0)
        {
            return null;
        }

        var closeFence = FindFence(lines, openFence + 1);
        if (closeFence < 0)
        {
            // Un bloque sin cerrar es una respuesta truncada. Escribir lo que llegó dejaría el
            // archivo a medias, que es peor que no hacer nada.
            return null;
        }

        var content = new StringBuilder();
        for (var index = openFence + 1; index < closeFence; index++)
        {
            content.Append(lines[index]).Append(Environment.NewLine);
        }

        var text = content.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            // Un bloque vacío borraría el archivo sin decirlo. Si de verdad hay que vaciarlo, que se
            // pida de otra forma y se vea.
            return null;
        }

        return new WorkspaceEdit(
            relativePath,
            text,
            string.IsNullOrWhiteSpace(reason) ? "Cambio propuesto por Kohana." : reason);
    }

    /// <summary>
    /// Devuelve el índice del marcador solo si aparece UNA vez. Dos cambios en una respuesta serían
    /// dos pasos, y el nivel 4 es "un único paso confirmado": ante dos, no se aplica ninguno.
    /// </summary>
    private static int IndexOfSingleMarker(string[] lines, string marker)
    {
        var found = -1;

        for (var index = 0; index < lines.Length; index++)
        {
            if (!lines[index].TrimStart().StartsWith(marker, StringComparison.Ordinal))
            {
                continue;
            }

            if (found >= 0)
            {
                return -1;
            }

            found = index;
        }

        return found;
    }

    private static int FindFence(string[] lines, int from)
    {
        for (var index = from; index < lines.Length; index++)
        {
            if (lines[index].TrimEnd().TrimStart().StartsWith(Fence, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
