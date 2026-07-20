using Nexo.Core.Updates;

namespace Nexo.Core.Tests;

public sealed class ReleaseVersionTests
{
    [Theory]
    [InlineData("v0.9.0-beta", 0, 9, 0, "beta")]
    [InlineData("1.2.3", 1, 2, 3, "")]
    [InlineData("2.4", 2, 4, 0, "")]
    [InlineData("v1.0.0-beta.2+build.7", 1, 0, 0, "beta.2")]
    public void TryParse_ValidVersion_ReturnsParts(
        string value,
        int major,
        int minor,
        int patch,
        string preRelease)
    {
        var parsed = ReleaseVersion.TryParse(value, out var version);

        Assert.True(parsed);
        Assert.Equal(major, version.Major);
        Assert.Equal(minor, version.Minor);
        Assert.Equal(patch, version.Patch);
        Assert.Equal(preRelease, version.PreRelease);
    }

    [Theory]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("1")]
    [InlineData("1.x.0")]
    public void TryParse_InvalidVersion_ReturnsFalse(string value) =>
        Assert.False(ReleaseVersion.TryParse(value, out _));

    [Fact]
    public void CompareTo_StableVersionIsNewerThanPreRelease()
    {
        ReleaseVersion.TryParse("1.0.0", out var stable);
        ReleaseVersion.TryParse("1.0.0-beta", out var beta);

        Assert.True(stable.CompareTo(beta) > 0);
    }

    [Fact]
    public void CompareTo_HigherPreReleaseNumberIsNewer()
    {
        ReleaseVersion.TryParse("1.0.0-beta.10", out var newer);
        ReleaseVersion.TryParse("1.0.0-beta.2", out var older);

        Assert.True(newer.CompareTo(older) > 0);
    }

    [Fact]
    public void ToString_NormalizesLeadingVAndBuildMetadata()
    {
        ReleaseVersion.TryParse("v0.9.0-beta+sha.123", out var version);

        Assert.Equal("0.9.0-beta", version.ToString());
    }
}
