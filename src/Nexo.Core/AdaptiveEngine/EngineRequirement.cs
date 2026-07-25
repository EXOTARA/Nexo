namespace Nexo.Core.AdaptiveEngine;

public sealed record EngineRequirement(
    string Description,
    EngineCostLevel CpuCost,
    EngineCostLevel RamCost,
    EngineCostLevel GpuCost,
    EngineCostLevel EnergyCost);
