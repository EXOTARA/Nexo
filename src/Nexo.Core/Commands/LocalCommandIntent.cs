namespace Nexo.Core.Commands;

public sealed record LocalCommandIntent(
    LocalCommandType Type,
    string? Target = null,
    double? Percent = null,
    double? Factor = null,
    double? DeltaPoints = null);
