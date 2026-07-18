namespace Nexo.Core.Audio;

public interface IAudioMixerService
{
    AudioMixerSnapshot ReadSnapshot();

    AudioActionResult SetMasterVolume(double percent);

    AudioActionResult SetMasterMuted(bool muted);

    AudioActionResult SetSessionVolume(string sessionId, double percent);

    AudioActionResult SetSessionMuted(string sessionId, bool muted);

    AudioActionResult SetApplicationVolume(string target, double percent);

    AudioActionResult ScaleApplicationVolume(string target, double factor);

    AudioActionResult ChangeApplicationVolume(string target, double deltaPoints);

    AudioActionResult SetApplicationMuted(string target, bool muted);

    AudioActionResult LowerAllExcept(string target, double factor);
}
