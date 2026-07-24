using Nexo.Core.Voice;

namespace Nexo.Core.Tests;

public sealed class VoiceUtteranceEndPolicyTests
{
    [Fact]
    public void ShouldComplete_WaitsForEnoughSpeech()
    {
        var snapshot = new VoiceUtteranceTimingSnapshot(
            SpeechDetected: true,
            SpeechMilliseconds: 200,
            TrailingSilenceMilliseconds: 2_000,
            LiveAudioMilliseconds: 3_000);

        Assert.False(VoiceUtteranceEndPolicy.ShouldComplete(
            snapshot,
            TimeSpan.FromMilliseconds(1_500)));
    }

    [Fact]
    public void ShouldComplete_DoesNotCutOnBriefPause()
    {
        var snapshot = new VoiceUtteranceTimingSnapshot(
            SpeechDetected: true,
            SpeechMilliseconds: 900,
            TrailingSilenceMilliseconds: 900,
            LiveAudioMilliseconds: 3_000);

        Assert.False(VoiceUtteranceEndPolicy.ShouldComplete(
            snapshot,
            TimeSpan.FromMilliseconds(1_500)));
    }

    [Fact]
    public void ShouldComplete_EndsAfterConfirmedSpeechAndTrailingSilence()
    {
        var snapshot = new VoiceUtteranceTimingSnapshot(
            SpeechDetected: true,
            SpeechMilliseconds: 1_200,
            TrailingSilenceMilliseconds: 1_500,
            LiveAudioMilliseconds: 3_000);

        Assert.True(VoiceUtteranceEndPolicy.ShouldComplete(
            snapshot,
            TimeSpan.FromMilliseconds(1_500)));
    }


    [Fact]
    public void ShouldComplete_ProtectsTheInitialListeningWindow()
    {
        var snapshot = new VoiceUtteranceTimingSnapshot(
            SpeechDetected: true,
            SpeechMilliseconds: 900,
            TrailingSilenceMilliseconds: 1_500,
            LiveAudioMilliseconds: 1_800);

        Assert.False(VoiceUtteranceEndPolicy.ShouldComplete(
            snapshot,
            TimeSpan.FromMilliseconds(1_500)));
    }

    [Fact]
    public void ShouldComplete_RejectsUnsafeTrailingSilence()
    {
        var snapshot = new VoiceUtteranceTimingSnapshot(
            SpeechDetected: true,
            SpeechMilliseconds: 1_000,
            TrailingSilenceMilliseconds: 1_000,
            LiveAudioMilliseconds: 3_000);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VoiceUtteranceEndPolicy.ShouldComplete(
                snapshot,
                TimeSpan.FromMilliseconds(250)));
    }
}
