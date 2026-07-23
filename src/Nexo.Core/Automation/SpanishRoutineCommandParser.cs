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

        // "ejecuta la rutina estudio" nombra el dominio: la intención es inequívoca.
        var explicitRunMatch = ExplicitRunRegex().Match(normalized);
        if (explicitRunMatch.Success)
        {
            return new RoutineCommand(
                RoutineCommandType.RunRoutine,
                original,
                explicitRunMatch.Groups["name"].Value.Trim(),
                RoutineMatchConfidence.Explicit);
        }

        // "inicia X" solo comparte el verbo con enfoque y tareas. Se devuelve como hipótesis;
        // `PromptDispatchPolicy` decide si gana. Ver defecto D1 de la fase 1.1.
        var runMatch = RunRegex().Match(normalized);
        if (runMatch.Success)
        {
            return new RoutineCommand(
                RoutineCommandType.RunRoutine,
                original,
                runMatch.Groups["name"].Value.Trim(),
                RoutineMatchConfidence.Inferred);
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

    [GeneratedRegex(@"^(?:ejecuta|inicia|activa|corre)\s+(?:la\s+|mi\s+)?rutina\s+(?:llamada\s+|que\s+se\s+llama\s+)?(?<name>.+)$")]
    private static partial Regex ExplicitRunRegex();

    [GeneratedRegex(@"^(?:ejecuta|inicia|activa|corre)\s+(?:la\s+)?(?<name>.+)$")]
    private static partial Regex RunRegex();

    [GeneratedRegex(@"^modo\s+(?<name>.+)$")]
    private static partial Regex ModeRegex();
}
