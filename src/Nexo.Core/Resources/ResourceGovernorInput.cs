using Nexo.Core.Metrics;

namespace Nexo.Core.Resources;

public sealed record ResourceGovernorInput(
    SystemSnapshot Snapshot,
    bool IsForegroundFullScreen,
    string? ForegroundProcessName,
    string? ForegroundWindowTitle,
    bool IsOnBattery);
