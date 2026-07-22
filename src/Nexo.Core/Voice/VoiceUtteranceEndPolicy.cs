namespace Nexo.Core.Voice;

public readonly record struct VoiceUtteranceTimingSnapshot(
    bool SpeechDetected,
    int SpeechMilliseconds,
    int TrailingSilenceMilliseconds,
    int LiveAudioMilliseconds);

public static class VoiceUtteranceEndPolicy
{
    public const int MinimumSpeechMilliseconds = 350;
    public const int MinimumLiveAudioMilliseconds = 2_500;

    public static bool ShouldComplete(
        VoiceUtteranceTimingSnapshot snapshot,
        TimeSpan requiredTrailingSilence)
    {
        if (requiredTrailingSilence < TimeSpan.FromMilliseconds(300))
        {
            throw new ArgumentOutOfRangeException(nameof(requiredTrailingSilence));
        }

        return snapshot.SpeechDetected &&
               snapshot.SpeechMilliseconds >= MinimumSpeechMilliseconds &&
               snapshot.LiveAudioMilliseconds >= MinimumLiveAudioMilliseconds &&
               snapshot.TrailingSilenceMilliseconds >=
                   requiredTrailingSilence.TotalMilliseconds;
    }
}
