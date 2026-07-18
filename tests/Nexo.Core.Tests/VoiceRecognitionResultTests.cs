using Nexo.Core.Voice;

namespace Nexo.Core.Tests;

public sealed class VoiceRecognitionResultTests
{
    [Fact]
    public void Recognized_TrimsTextAndClampsConfidence()
    {
        var result = VoiceRecognitionResult.Recognized("  baja Spotify  ", 1.4);

        Assert.True(result.IsRecognized);
        Assert.Equal("baja Spotify", result.Text);
        Assert.Equal(1, result.Confidence);
    }

    [Fact]
    public void NoSpeech_DoesNotReturnPromptText()
    {
        var result = VoiceRecognitionResult.NoSpeech("No detecté una orden clara.");

        Assert.False(result.IsRecognized);
        Assert.Empty(result.Text);
        Assert.Equal(0, result.Confidence);
    }
}
