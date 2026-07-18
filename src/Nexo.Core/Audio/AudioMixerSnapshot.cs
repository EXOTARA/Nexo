namespace Nexo.Core.Audio;

public sealed record AudioMixerSnapshot(
    bool IsAvailable,
    string DeviceName,
    double MasterVolumePercent,
    bool IsMasterMuted,
    IReadOnlyList<AudioSessionSnapshot> Sessions,
    string? ErrorMessage = null)
{
    public static AudioMixerSnapshot Unavailable(string message) =>
        new(
            false,
            "Sin dispositivo",
            0,
            false,
            Array.Empty<AudioSessionSnapshot>(),
            message);
}
