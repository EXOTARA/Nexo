namespace Nexo.Core.AdaptiveEngine;

public sealed record EngineRuntimeState(
    EngineIdentifier EngineId,
    bool? IsAvailable,
    bool? IsConfigured,
    bool? IsActive,
    string? Detail);
