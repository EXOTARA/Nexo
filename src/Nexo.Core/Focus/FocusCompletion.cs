namespace Nexo.Core.Focus;

public sealed record FocusCompletion(
    string Label,
    FocusSessionKind Kind,
    TimeSpan Duration,
    DateTimeOffset CompletedAt);
