namespace Nexo.Core.Ambient;

public sealed record AmbientRequestResult(
    string ShortText,
    string? ExpandedText,
    IReadOnlyList<AmbientQuickAction> QuickActions,
    bool CanUndo);
