namespace Nexo.Core.Ambient;

public sealed class AmbientRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Prompt { get; set; } = string.Empty;

    public AmbientRequestStatus Status { get; set; } = AmbientRequestStatus.Listening;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    public AmbientContextSnapshot? Context { get; set; }

    public AmbientRequestResult? Result { get; set; }

    /// <summary>Diseño D7 — texto acumulado mientras la respuesta se recibe por partes.</summary>
    public string? PartialText { get; set; }

    public string? ErrorMessage { get; set; }

    public bool Undone { get; set; }

    public AmbientRequest Copy() => new()
    {
        Id = Id,
        Prompt = Prompt,
        Status = Status,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt,
        Context = Context,
        Result = Result,
        PartialText = PartialText,
        ErrorMessage = ErrorMessage,
        Undone = Undone
    };
}
