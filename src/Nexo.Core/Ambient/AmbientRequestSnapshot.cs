namespace Nexo.Core.Ambient;

public sealed record AmbientRequestSnapshot(
    AmbientRequest? ActiveRequest,
    IReadOnlyList<AmbientRequestHistoryEntry> RecentHistory);
