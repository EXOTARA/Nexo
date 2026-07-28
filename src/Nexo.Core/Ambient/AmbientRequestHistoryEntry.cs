namespace Nexo.Core.Ambient;

public sealed class AmbientRequestHistoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Prompt { get; set; } = string.Empty;

    public AmbientRequestStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset CompletedAt { get; set; }

    public string? ResultShortText { get; set; }

    public string? ErrorMessage { get; set; }

    public bool CanUndo { get; set; }

    public bool Undone { get; set; }

    public AmbientRequestHistoryEntry Copy() => new()
    {
        Id = Id,
        Prompt = Prompt,
        Status = Status,
        CreatedAt = CreatedAt,
        CompletedAt = CompletedAt,
        ResultShortText = ResultShortText,
        ErrorMessage = ErrorMessage,
        CanUndo = CanUndo,
        Undone = Undone
    };
}
