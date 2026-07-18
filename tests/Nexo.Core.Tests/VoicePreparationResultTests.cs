using Nexo.Core.Voice;

namespace Nexo.Core.Tests;

public sealed class VoicePreparationResultTests
{
    [Fact]
    public void Ready_UsesFriendlyDefaultDetail()
    {
        var result = VoicePreparationResult.Ready();

        Assert.True(result.IsReady);
        Assert.Equal("Voz local lista.", result.Detail);
    }

    [Fact]
    public void Unavailable_TrimsProvidedDetail()
    {
        var result = VoicePreparationResult.Unavailable("  Sin conexión.  ");

        Assert.False(result.IsReady);
        Assert.Equal("Sin conexión.", result.Detail);
    }

    [Fact]
    public void Downloading_ClampsNegativeByteCount()
    {
        var progress = VoicePreparationProgress.Downloading(-50);

        Assert.Equal(0, progress.BytesDownloaded);
        Assert.Contains("0 MB", progress.Detail);
    }
}
