namespace Nexo.Core.Ambient;

public sealed record AmbientQuickAction(
    string Id,
    string Label,
    AmbientAutonomyLevel Level,
    bool RequiresConfirmation = false);
