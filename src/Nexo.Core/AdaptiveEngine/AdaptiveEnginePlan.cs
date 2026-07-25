using Nexo.Core.Hardware;

namespace Nexo.Core.AdaptiveEngine;

public sealed record AdaptiveEnginePlan(
    HardwarePerformanceMode Mode,
    HardwareCapabilityTier Tier,
    HardwareDataConfidence Confidence,
    string Summary,
    IReadOnlyList<EngineRecommendation> Recommendations,
    IReadOnlyList<string> GeneralWarnings,
    IReadOnlyList<string> AvailableTodayChanges,
    IReadOnlyList<string> FutureChanges,
    DateTimeOffset EvaluatedAt);
