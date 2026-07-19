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


    [Fact]
    public void Recognized_CanRequireConfirmation()
    {
        var result = VoiceRecognitionResult.Recognized(
            "baja spotify",
            0.52,
            requiresConfirmation: true,
            detail: "La orden se escuchó con poca claridad.");

        Assert.True(result.IsRecognized);
        Assert.True(result.RequiresConfirmation);
        Assert.Equal(0.52, result.Confidence);
    }

    [Fact]
    public void NoSpeech_NeverRequiresConfirmation()
    {
        var result = VoiceRecognitionResult.NoSpeech("No escuché suficiente audio.");

        Assert.False(result.RequiresConfirmation);
    }
}
