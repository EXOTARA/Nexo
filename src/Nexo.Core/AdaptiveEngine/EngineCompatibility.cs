namespace Nexo.Core.AdaptiveEngine;

public sealed record EngineCompatibility(
    EngineIdentifier EngineId,
    bool IsCompatible,
    IReadOnlyList<string> Reasons);
