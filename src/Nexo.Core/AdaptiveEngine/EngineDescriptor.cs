namespace Nexo.Core.AdaptiveEngine;

public sealed record EngineDescriptor(
    EngineIdentifier Id,
    string DisplayName,
    EngineCategory Category,
    bool? IsLocal,
    EngineRequirement MinimumRequirement,
    EngineRequirement RecommendedRequirement,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> Limitations,
    bool AllowsRuntimeSelection,
    bool RequiresRestart,
    bool RequiresDownload,
    bool IncludedWithKohana);
