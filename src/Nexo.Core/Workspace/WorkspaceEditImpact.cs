namespace Nexo.Core.Workspace;

/// <summary>
/// Qué le pasa de verdad a un archivo si se aplica un cambio propuesto: cuántas líneas se añaden,
/// cuántas desaparecen, y cuáles son las que desaparecen.
/// </summary>
public sealed record WorkspaceEditImpact(
    int LinesBefore,
    int LinesAfter,
    IReadOnlyList<string> RemovedLines)
{
    /// <summary>Quitar contenido es lo único que no se puede reconstruir mirando el resultado.</summary>
    public bool RemovesContent => RemovedLines.Count > 0;
}

/// <summary>
/// Diseño D14 — corrige un hueco encontrado usando la app, no leyéndola. El formato KOHANA-EDIT pide
/// el archivo COMPLETO, y la salvaguarda añadida tras el borrado del README comprueba que el
/// contenido actual viajó en el contexto de ese turno. Pero eso solo verifica lo que el modelo
/// <i>recibió</i>: nada comprobaba lo que el modelo <i>devolvió</i>.
///
/// Visto en una prueba a mano: tras un cambio aplicado, el usuario añadió una línea suya al final;
/// en la siguiente petición el modelo leyó el archivo, conservó lo demás y soltó esa línea sin
/// mencionarlo. La confirmación decía "Voy a REEMPLAZAR" y el tamaño en caracteres — con eso nadie,
/// y menos alguien no técnico, puede notar que va a perder una línea propia.
///
/// La comparación es por líneas y por contenido, no un diff posicional: mover una línea de sitio no
/// es perderla, y avisar de eso sería ruido que enseña a ignorar el aviso.
/// </summary>
public static class WorkspaceEditImpactAnalyzer
{
    /// <summary>Cuántas líneas perdidas se enumeran antes de resumir el resto.</summary>
    public const int MaximumListedRemovals = 5;

    public static WorkspaceEditImpact Compare(string? currentContent, string? newContent)
    {
        var before = SplitLines(currentContent);
        var after = SplitLines(newContent);

        // Multiconjunto: si una línea aparecía tres veces y ahora aparece una, se han perdido dos.
        var remaining = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in after)
        {
            remaining[line] = remaining.GetValueOrDefault(line) + 1;
        }

        var removed = new List<string>();
        foreach (var line in before)
        {
            if (remaining.TryGetValue(line, out var count) && count > 0)
            {
                remaining[line] = count - 1;
                continue;
            }

            // Una línea en blanco de más o de menos no es pérdida de contenido de nadie.
            if (!string.IsNullOrWhiteSpace(line))
            {
                removed.Add(line);
            }
        }

        return new WorkspaceEditImpact(before.Length, after.Length, removed);
    }

    /// <summary>
    /// El aviso que se le enseña a la persona antes de confirmar. Devuelve <c>null</c> si el cambio
    /// no quita nada: avisar de lo que no pasa quita fuerza al aviso de lo que sí.
    /// </summary>
    public static string? DescribeRemovals(WorkspaceEditImpact impact)
    {
        if (impact is null || !impact.RemovesContent)
        {
            return null;
        }

        var listed = impact.RemovedLines
            .Take(MaximumListedRemovals)
            .Select(line => "    " + Truncate(line.Trim(), 70));

        var text = $"ATENCIÓN: esto QUITA {impact.RemovedLines.Count} línea(s) que hay ahora:" +
                   Environment.NewLine + string.Join(Environment.NewLine, listed);

        var hidden = impact.RemovedLines.Count - MaximumListedRemovals;
        if (hidden > 0)
        {
            text += Environment.NewLine + $"    (y {hidden} línea(s) más)";
        }

        return text;
    }

    private static string[] SplitLines(string? content) =>
        string.IsNullOrEmpty(content)
            ? []
            : content.ReplaceLineEndings("\n").Split('\n');

    private static string Truncate(string value, int maximum) =>
        value.Length <= maximum ? value : value[..maximum] + "…";
}
