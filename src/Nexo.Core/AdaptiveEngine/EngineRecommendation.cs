namespace Nexo.Core.AdaptiveEngine;

public sealed record EngineRecommendation(
    EngineCategory Category,
    EngineIdentifier? ConfiguredEngineId,
    EngineIdentifier? ActiveEngineId,
    EngineIdentifier? RecommendedEngineId,
    IReadOnlyList<EngineCompatibility> CompatibleAlternatives,
    IReadOnlyList<EngineCompatibility> IncompatibleEngines,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string> Warnings,
    bool IsRecommendationOnly);
