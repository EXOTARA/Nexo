using System.Globalization;
using System.Text.RegularExpressions;
using Nexo.Core.Commands;

namespace Nexo.Core.Tasks;

public sealed partial class SpanishTaskCommandParser
{
    private static readonly IReadOnlyDictionary<string, DayOfWeek> Weekdays =
        new Dictionary<string, DayOfWeek>(StringComparer.OrdinalIgnoreCase)
        {
            ["lunes"] = DayOfWeek.Monday,
            ["martes"] = DayOfWeek.Tuesday,
            ["miercoles"] = DayOfWeek.Wednesday,
            ["jueves"] = DayOfWeek.Thursday,
            ["viernes"] = DayOfWeek.Friday,
            ["sabado"] = DayOfWeek.Saturday,
            ["domingo"] = DayOfWeek.Sunday
        };

    private static readonly IReadOnlyDictionary<string, int> HourWords =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["una"] = 1,
            ["uno"] = 1,
            ["dos"] = 2,
            ["tres"] = 3,
            ["cuatro"] = 4,
            ["cinco"] = 5,
            ["seis"] = 6,
            ["siete"] = 7,
            ["ocho"] = 8,
            ["nueve"] = 9,
            ["diez"] = 10,
            ["once"] = 11,
            ["doce"] = 12
        };

    public TaskCommand Parse(string? rawText, DateTimeOffset now)
    {
        var original = rawText?.Trim() ?? string.Empty;
        var normalized = NaturalCommandParser.Normalize(original);
        normalized = RemoveWakeWord(normalized);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return TaskCommand.None(original);
        }

        if (Matches(normalized,
                "abre tareas",
                "muestra tareas",
                "ve a tareas",
                "abre pendientes"))
        {
            return new TaskCommand(TaskCommandType.OpenTasks, original);
        }

        if (Matches(normalized,
                "que tengo pendiente hoy",
                "que tengo para hoy",
                "mis tareas de hoy",
                "muestra mis tareas de hoy",
                "tareas de hoy"))
        {
            return new TaskCommand(TaskCommandType.ListToday, original);
        }

        if (Matches(normalized,
                "que tengo pendiente",
                "que tareas tengo",
                "muestra mis tareas",
                "muestra pendientes",
                "mis pendientes"))
        {
            return new TaskCommand(TaskCommandType.ListPending, original);
        }var completionMatch = CompletePrefixRegex().Match(normalized);
if (completionMatch.Success)
{
    var completedTaskTitle =
        normalized[completionMatch.Length..];

    completedTaskTitle = Regex.Replace(
        completedTaskTitle,
        @"^(?:la\s+)?tarea(?:\s+de)?\s+",
        string.Empty,
        RegexOptions.IgnoreCase);

    completedTaskTitle = CleanQuery(completedTaskTitle);

    return string.IsNullOrWhiteSpace(completedTaskTitle)
        ? TaskCommand.None(original)
        : new TaskCommand(
            TaskCommandType.Complete,
            original,
            completedTaskTitle);
}

var deleteMatch = DeletePrefixRegex().Match(normalized);
if (deleteMatch.Success)
{
    var deletedTaskTitle =
        normalized[deleteMatch.Length..];

    deletedTaskTitle = Regex.Replace(
        deletedTaskTitle,
        @"^(?:(?:el|la)\s+)?(?:tarea|recordatorio)(?:\s+de)?\s+",
        string.Empty,
        RegexOptions.IgnoreCase);

    deletedTaskTitle = CleanQuery(deletedTaskTitle);

    return string.IsNullOrWhiteSpace(deletedTaskTitle)
        ? TaskCommand.None(original)
        : new TaskCommand(
            TaskCommandType.Delete,
            original,
            deletedTaskTitle);
}

        if (!CreatePrefixRegex().IsMatch(normalized))
        {
            return TaskCommand.None(original);
        }

        var reminderEnabled = normalized.StartsWith("recuerdame", StringComparison.OrdinalIgnoreCase) ||
                              normalized.StartsWith("recordarme", StringComparison.OrdinalIgnoreCase) ||
                              normalized.StartsWith("pon un recordatorio", StringComparison.OrdinalIgnoreCase) ||
                              normalized.StartsWith("crea un recordatorio", StringComparison.OrdinalIgnoreCase);

        var priority = ParsePriority(normalized);
        var dueAt = ParseDueAt(normalized, now);
        var title = ExtractTitle(original);

        return string.IsNullOrWhiteSpace(title)
            ? TaskCommand.None(original)
            : new TaskCommand(
                TaskCommandType.Create,
                original,
                title,
                dueAt,
                priority,
                reminderEnabled);
    }

    private static DateTimeOffset? ParseDueAt(
        string normalized,
        DateTimeOffset now)
    {
        DateTime? date = null;

        if (normalized.Contains("manana", StringComparison.OrdinalIgnoreCase))
        {
            date = now.Date.AddDays(1);
        }
        else if (normalized.Contains("hoy", StringComparison.OrdinalIgnoreCase))
        {
            date = now.Date;
        }
        else
        {
            foreach (var pair in Weekdays)
            {
                if (!Regex.IsMatch(normalized, $@"\b{pair.Key}\b", RegexOptions.IgnoreCase))
                {
                    continue;
                }

                date = NextWeekday(now.Date, pair.Value);
                break;
            }
        }

        var time = ParseTime(normalized);
        if (!date.HasValue && time.HasValue)
        {
            date = now.Date;
            if (date.Value.Add(time.Value) <= now.DateTime)
            {
                date = date.Value.AddDays(1);
            }
        }

        if (!date.HasValue)
        {
            return null;
        }

        var resolvedTime = time ?? new TimeSpan(9, 0, 0);
        var local = date.Value.Add(resolvedTime);
        return new DateTimeOffset(local, now.Offset);
    }

    private static TimeSpan? ParseTime(string normalized)
    {
        var match = TimeRegex().Match(normalized);
        if (!match.Success)
        {
            return null;
        }

        var hourToken = match.Groups["hour"].Value;
        var minuteToken = match.Groups["minute"].Value;
        var meridiem = match.Groups["meridiem"].Value;

        int hour;
        if (!int.TryParse(hourToken, NumberStyles.None, CultureInfo.InvariantCulture, out hour) &&
            !HourWords.TryGetValue(hourToken, out hour))
        {
            return null;
        }

        var minute = int.TryParse(minuteToken, out var parsedMinute)
            ? parsedMinute
            : 0;

        if (hour is < 0 or > 23 || minute is < 0 or > 59)
        {
            return null;
        }

        if ((meridiem is "pm" or "p m") && hour < 12)
        {
            hour += 12;
        }
        else if ((meridiem is "am" or "a m") && hour == 12)
        {
            hour = 0;
        }

        return new TimeSpan(hour, minute, 0);
    }

    private static string ExtractTitle(string original)
{
    var title = WakeWordOriginalRegex()
        .Replace(original.Trim(), string.Empty, 1);

    title = CreatePrefixOriginalRegex()
        .Replace(title, string.Empty, 1);

    title = DatePhraseOriginalRegex()
        .Replace(title, string.Empty);

    title = TimePhraseOriginalRegex()
        .Replace(title, string.Empty);

    title = PriorityOriginalRegex()
        .Replace(title, string.Empty);

    title = Regex.Replace(title, @"\s+", " ")
        .Trim(' ', '.', ',', ';', ':', '-');

    return title;
}

    private static TaskPriority ParsePriority(string normalized)
    {
        if (normalized.Contains("prioridad alta", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("urgente", StringComparison.OrdinalIgnoreCase))
        {
            return TaskPriority.High;
        }

        if (normalized.Contains("prioridad baja", StringComparison.OrdinalIgnoreCase))
        {
            return TaskPriority.Low;
        }

        return TaskPriority.Normal;
    }

    private static DateTime NextWeekday(DateTime start, DayOfWeek requested)
    {
        var difference = ((int)requested - (int)start.DayOfWeek + 7) % 7;
        return start.AddDays(difference == 0 ? 7 : difference);
    }

    private static string RemoveWakeWord(string normalized)
    {
        return Regex.Replace(
            normalized,
            @"^(?:oye\s+)?(?:nexo|exo)\s+",
            string.Empty,
            RegexOptions.IgnoreCase).Trim();
    }

    private static string CleanQuery(string title) =>
        Regex.Replace(title, @"\s+", " ").Trim(' ', '.', ',', ';', ':', '-');

    private static bool Matches(string value, params string[] options) =>
        options.Any(option => value.Equals(option, StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex(@"^(?:recuerdame|recordarme|agrega|anade|crea|crear|nueva tarea|apunta|pon un recordatorio|crea un recordatorio)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CreatePrefixRegex();

    [GeneratedRegex(
    @"^(?:marca|marcar)\s+(?:como\s+)?(?:terminada|completada|hecha)\s+",
    RegexOptions.IgnoreCase)]
    private static partial Regex CompletePrefixRegex();


    [GeneratedRegex(
    @"(?:a\s+las?|a\s+la)\s+(?<hour>\d{1,2}|una|uno|dos|tres|cuatro|cinco|seis|siete|ocho|nueve|diez|once|doce)(?:(?::|\s+)(?<minute>\d{2}))?\s*(?<meridiem>am|pm|a\s+m|p\s+m)?\b",
    RegexOptions.IgnoreCase)]
    private static partial Regex TimeRegex();

    [GeneratedRegex(@"^(?:recu[eé]rdame|recordarme|agrega|a[nñ]ade|crea(?:r)?(?:\s+una\s+tarea)?|nueva\s+tarea|apunta|pon\s+un\s+recordatorio|crea\s+un\s+recordatorio)\s+(?:que\s+)?", RegexOptions.IgnoreCase)]
    private static partial Regex CreatePrefixOriginalRegex();

    [GeneratedRegex(
    @"\b(?:para\s+)?(?:hoy|ma[nñ]ana|(?:el\s+)?(?:lunes|martes|mi[eé]rcoles|jueves|viernes|s[aá]bado|domingo))\b",
    RegexOptions.IgnoreCase)]
    private static partial Regex DatePhraseOriginalRegex();

    [GeneratedRegex(@"\b(?:a\s+las?|a\s+la)\s+(?:\d{1,2}|una|uno|dos|tres|cuatro|cinco|seis|siete|ocho|nueve|diez|once|doce)(?::\d{2})?\s*(?:a\.?\s?m\.?|p\.?\s?m\.?)?\b", RegexOptions.IgnoreCase)]
    private static partial Regex TimePhraseOriginalRegex();

    [GeneratedRegex(@"\b(?:con\s+)?(?:prioridad\s+(?:alta|media|normal|baja)|urgente)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PriorityOriginalRegex();

    [GeneratedRegex(
    @"^(?:oye\s+)?(?:nexo|exo)[\s,.:;!?-]+",
    RegexOptions.IgnoreCase)]
    private static partial Regex WakeWordOriginalRegex();

    [GeneratedRegex(
    @"^(?:cancela|cancelar|elimina|eliminar|borra|borrar|quita|quitar)\s+",
    RegexOptions.IgnoreCase)]
    private static partial Regex DeletePrefixRegex();
}
