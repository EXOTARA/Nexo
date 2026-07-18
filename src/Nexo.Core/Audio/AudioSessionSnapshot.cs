namespace Nexo.Core.Audio;

public sealed record AudioSessionSnapshot(
    string SessionId,
    int ProcessId,
    string ProcessName,
    string DisplayName,
    double VolumePercent,
    bool IsMuted,
    bool IsActive,
    bool IsSystemSounds);
