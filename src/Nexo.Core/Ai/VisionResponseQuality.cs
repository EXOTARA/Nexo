namespace Nexo.Core.Ai;

public static class VisionResponseQuality
{
    private static readonly string[] GenericDeflections =
    [
        "contacta al soporte",
        "contactar al soporte",
        "consulta con un profesional",
        "consulta a un profesional",
        "acude a un tecnico",
        "acude a un técnico",
        "reinstala el programa",
        "reinstalar el programa",
        "no puedo ayudarte"
    ];

    private static readonly string[] ActionSignals =
    [
        "reemplaza",
        "cambia",
        "quita",
        "elimina",
        "agrega",
        "añade",
        "ejecuta",
        "vuelve a ejecutar",
        "dotnet ",
        "powershell",
        "cómo comprobarlo",
        "como comprobarlo",
        "línea ",
        "linea "
    ];

    public static bool IsTooGeneric(
        string? response,
        VisionDiagnosticEvidence evidence)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return true;
        }

        var normalized = response.Trim();
        if (!evidence.ErrorVisible)
        {
            return false;
        }

        var hasDeflection = GenericDeflections.Any(phrase =>
            normalized.Contains(phrase, StringComparison.OrdinalIgnoreCase));
        var hasConcreteEvidence = ContainsEvidence(normalized, evidence);
        var hasAction = ActionSignals.Any(signal =>
            normalized.Contains(signal, StringComparison.OrdinalIgnoreCase));

        return (hasDeflection && !hasConcreteEvidence && !hasAction) ||
               (normalized.Length < 120 && !hasConcreteEvidence && !hasAction);
    }

    private static bool ContainsEvidence(
        string response,
        VisionDiagnosticEvidence evidence)
    {
        return (!string.IsNullOrWhiteSpace(evidence.ErrorCode) &&
                response.Contains(evidence.ErrorCode, StringComparison.OrdinalIgnoreCase)) ||
               (!string.IsNullOrWhiteSpace(evidence.FileName) &&
                response.Contains(evidence.FileName, StringComparison.OrdinalIgnoreCase)) ||
               (evidence.LineNumber is { } line &&
                response.Contains(line.ToString(), StringComparison.OrdinalIgnoreCase));
    }
}
