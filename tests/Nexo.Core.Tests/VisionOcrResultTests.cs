using Nexo.Core.Vision;

namespace Nexo.Core.Tests;

public sealed class VisionOcrResultTests
{
    [Fact]
    public void Success_TrimsTextAndClampsConfidence()
    {
        var result = VisionOcrResult.Success("  texto visible  ", 2);

        Assert.True(result.IsSuccess);
        Assert.Equal("texto visible", result.Text);
        Assert.Equal(1, result.Confidence);
    }

    [Fact]
    public void Failed_DoesNotExposeText()
    {
        var result = VisionOcrResult.Failed("OCR no disponible");

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Text);
        Assert.Equal(0, result.Confidence);
    }
}
