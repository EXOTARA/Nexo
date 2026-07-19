namespace Nexo.Core.Ai;

public sealed record VisionDiagnosticEvidence
{
    public bool ErrorVisible { get; init; }

    public string ProblemType { get; init; } = string.Empty;

    public string ErrorCode { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public int? LineNumber { get; init; }

    public string VisibleMessage { get; init; } = string.Empty;

    public string VisibleCommand { get; init; } = string.Empty;

    public string RelevantCode { get; init; } = string.Empty;

    public string MissingInformation { get; init; } = string.Empty;

    public double Confidence { get; init; }

    public bool HasActionableEvidence =>
        ErrorVisible &&
        (!string.IsNullOrWhiteSpace(ErrorCode) ||
         !string.IsNullOrWhiteSpace(VisibleMessage) ||
         !string.IsNullOrWhiteSpace(FileName));

    public VisionDiagnosticEvidence Normalize()
    {
        return this with
        {
            ProblemType = Clean(ProblemType),
            ErrorCode = Clean(ErrorCode),
            FileName = Clean(FileName),
            LineNumber = LineNumber is > 0 ? LineNumber : null,
            VisibleMessage = Clean(VisibleMessage),
            VisibleCommand = Clean(VisibleCommand),
            RelevantCode = Clean(RelevantCode),
            MissingInformation = Clean(MissingInformation),
            Confidence = Math.Clamp(Confidence, 0d, 1d)
        };
    }

    public string BuildCompactSummary()
    {
        var normalized = Normalize();
        if (!normalized.ErrorVisible)
        {
            return string.IsNullOrWhiteSpace(normalized.MissingInformation)
                ? "No encontré un error legible en la captura."
                : $"No encontré un error legible. Falta: {normalized.MissingInformation}";
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(normalized.ErrorCode))
        {
            parts.Add(normalized.ErrorCode);
        }

        if (!string.IsNullOrWhiteSpace(normalized.FileName))
        {
            parts.Add(normalized.LineNumber is { } line
                ? $"{normalized.FileName} · línea {line}"
                : normalized.FileName);
        }
        else if (normalized.LineNumber is { } line)
        {
            parts.Add($"línea {line}");
        }

        if (!string.IsNullOrWhiteSpace(normalized.VisibleMessage))
        {
            parts.Add(normalized.VisibleMessage);
        }

        return parts.Count == 0
            ? "Se detectó un problema, pero el texto no fue suficientemente legible."
            : string.Join(" · ", parts);
    }

    private static string Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
