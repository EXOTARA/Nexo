using Nexo.Core.Audio;

namespace Nexo.Core.Tests;

public sealed class AudioTargetMatcherTests
{
    [Theory]
    [InlineData("discord", "Discord", "Discord", true)]
    [InlineData("spotify", "Spotify", "Spotify Premium", true)]
    [InlineData("zen", "zen", "Zen Browser", true)]
    [InlineData("edge", "msedge", "Microsoft Edge", true)]
    [InlineData("firefox", "chrome", "Google Chrome", false)]
    public void IsMatch_UsesProcessAndFriendlyNames(
        string target,
        string processName,
        string displayName,
        bool expected)
    {
        var result = AudioTargetMatcher.IsMatch(target, processName, displayName);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Normalize_RemovesAccentsAndExtraSymbols()
    {
        var result = AudioTargetMatcher.Normalize("  Música—Aplicación  ");

        Assert.Equal("musica aplicacion", result);
    }
}
