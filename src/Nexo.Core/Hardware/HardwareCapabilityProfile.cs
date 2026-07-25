namespace Nexo.Core.Hardware;

public sealed record HardwareCapabilityProfile(
    HardwareCapabilitySnapshot Snapshot,
    HardwareCapabilityTier Tier,
    string Summary,
    IReadOnlyList<HardwareCapabilityReason> PositiveReasons,
    IReadOnlyList<HardwareCapabilityReason> Limitations,
    IReadOnlyList<HardwareCapabilityReason> MissingData,
    HardwareDataConfidence OverallConfidence)
{
    public bool HasCompleteData => MissingData.Count == 0;
}
