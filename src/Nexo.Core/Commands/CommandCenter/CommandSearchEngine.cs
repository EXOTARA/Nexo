using System.Globalization;
using System.Text;

namespace Nexo.Core.Commands.CommandCenter;

/// <summary>
/// Búsqueda y ordenación de comandos del Sakura Command Center.
///
/// Es lógica pura y determinista: mismos comandos y misma consulta producen siempre el mismo
/// orden. No usa reflexión (se ejecuta en cada pulsación de tecla) ni construye vistas.
///
/// La normalización quita acentos además de mayúsculas y espacios sobrantes, porque los títulos
/// están en español: escribir "enfoque" debe encontrar "Enfoque" y escribir "personalizar" debe
/// encontrar "Personalización".
/// </summary>
public static class CommandSearchEngine
{
    private const int ExactTitleScore = 1000;
    private const int TitleStartsWithScore = 800;
    private const int TitleContainsScore = 600;
    private const int KeywordExactScore = 500;
    private const int KeywordStartsWithScore = 400;
    private const int KeywordContainsScore = 300;
    private const int DescriptionContainsScore = 150;

    /// <summary>
    /// Devuelve los comandos que coinciden con <paramref name="query"/>, mejor primero. Con una
    /// consulta vacía devuelve todos en su orden de registro, que es el comportamiento esperado
    /// al abrir la paleta sin escribir nada.
    /// </summary>
    public static IReadOnlyList<KohanaCommandDescriptor> Search(
        IReadOnlyList<KohanaCommandDescriptor> commands,
        string? query)
    {
        ArgumentNullException.ThrowIfNull(commands);

        var normalizedQuery = Normalize(query);
        if (normalizedQuery.Length == 0)
        {
            return commands;
        }

        var matches = new List<(KohanaCommandDescriptor Command, int Score, int Order)>();
        for (var index = 0; index < commands.Count; index++)
        {
            var command = commands[index];
            var score = Score(command, normalizedQuery);
            if (score > 0)
            {
                matches.Add((command, score, index));
            }
        }

        return
        [
            .. matches
                .OrderByDescending(match => match.Score)
                // Entre iguales gana el título más corto (más específico) y, si empatan, el orden
                // de registro: nunca un orden arbitrario que cambie entre ejecuciones.
                .ThenBy(match => match.Command.Title.Length)
                .ThenBy(match => match.Order)
                .Select(match => match.Command)
        ];
    }

    /// <summary>
    /// Puntuación de un comando frente a una consulta ya normalizada. Cero significa "no coincide".
    /// El título pesa más que las palabras clave, y estas más que la descripción.
    /// </summary>
    public static int Score(KohanaCommandDescriptor command, string normalizedQuery)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrEmpty(normalizedQuery))
        {
            return 0;
        }

        var title = Normalize(command.Title);
        if (title == normalizedQuery)
        {
            return ExactTitleScore;
        }

        if (title.StartsWith(normalizedQuery, StringComparison.Ordinal))
        {
            return TitleStartsWithScore;
        }

        var best = 0;
        if (title.Contains(normalizedQuery, StringComparison.Ordinal))
        {
            best = TitleContainsScore;
        }

        foreach (var keyword in command.Keywords)
        {
            var normalizedKeyword = Normalize(keyword);
            if (normalizedKeyword.Length == 0)
            {
                continue;
            }

            if (normalizedKeyword == normalizedQuery)
            {
                best = Math.Max(best, KeywordExactScore);
            }
            else if (normalizedKeyword.StartsWith(normalizedQuery, StringComparison.Ordinal))
            {
                best = Math.Max(best, KeywordStartsWithScore);
            }
            else if (normalizedKeyword.Contains(normalizedQuery, StringComparison.Ordinal))
            {
                best = Math.Max(best, KeywordContainsScore);
            }
        }

        if (best == 0 && Normalize(command.Description).Contains(normalizedQuery, StringComparison.Ordinal))
        {
            best = DescriptionContainsScore;
        }

        return best;
    }

    /// <summary>
    /// Minúsculas, sin acentos y con los espacios internos colapsados. Permite que "  ENFOQUE  ",
    /// "enfoque" y "Enfoque" sean la misma consulta.
    /// </summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var collapsed = string.Join(
            ' ',
            value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var decomposed = collapsed.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
