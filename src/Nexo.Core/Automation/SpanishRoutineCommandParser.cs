using System.Text.RegularExpressions;

namespace Nexo.Core.Automation;

public sealed partial class SpanishRoutineCommandParser
{
    public RoutineCommand Parse(string? rawText)
    {
        var original = rawText?.Trim() ?? string.Empty;
        var normalized = RoutineText.Normalize(original);
        normalized = WakeWordRegex().Replace(normalized, string.Empty, 1).Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return RoutineCommand.None(original);
        }

        if (OpenRegex().IsMatch(normalized))
        {
            return new RoutineCommand(RoutineCommandType.OpenRoutines, original);
        }

        if (ListRegex().IsMatch(normalized))
        {
            return new RoutineCommand(RoutineCommandType.ListRoutines, original);
        }

        var runMatch = RunRegex().Match(normalized);
        if (runMatch.Success)
        {
            return new RoutineCommand(
                RoutineCommandType.RunRoutine,
                original,
                runMatch.Groups["name"].Value.Trim());
        }

        var modeMatch = ModeRegex().Match(normalized);
        if (modeMatch.Success)
        {
            return new RoutineCommand(
                RoutineCommandType.RunRoutine,
                original,
                $"modo {modeMatch.Groups["name"].Value.Trim()}");
        }

        return RoutineCommand.None(original);
    }

    [GeneratedRegex(@"^(?:(?:oye|hey)\s+)?(?:kohana|nexo|exo)\s+")]
    private static partial Regex WakeWordRegex();

    [GeneratedRegex(@"^(?:abre|muestra|ve\s+a)\s+(?:mis\s+|las\s+)?rutinas$")]
    private static partial Regex OpenRegex();

    [GeneratedRegex(@"^(?:lista|enumera|cuales\s+son|que)\s+(?:mis\s+|las\s+)?rutinas(?:\s+disponibles)?$")]
    private static partial Regex ListRegex();

    [GeneratedRegex(@"^(?:ejecuta|inicia|activa|corre)\s+(?:la\s+)?(?:rutina\s+)?(?<name>.+)$")]
    private static partial Regex RunRegex();

    [GeneratedRegex(@"^modo\s+(?<name>.+)$")]
    private static partial Regex ModeRegex();
}
