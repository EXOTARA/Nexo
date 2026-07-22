using Nexo.Core.Voice;

namespace Nexo.Core.Tests;

public sealed class WakeWordAliasPolicyTests
{
    [Theory]
    [InlineData("  CÓYANA  ", "coyana")]
    [InlineData("ko yana", "ko yana")]
    public void TryNormalize_AcceptsShortPersonalAlias(string value, string expected)
    {
        Assert.True(WakeWordAliasPolicy.TryNormalize(value, out var normalized, out _));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("nexo")]
    [InlineData("oye nexo")]
    [InlineData("")]
    public void TryNormalize_RejectsLegacyOrEmptyAlias(string value)
    {
        Assert.False(WakeWordAliasPolicy.TryNormalize(value, out _, out _));
    }

    [Fact]
    public void NormalizeMany_RemovesDuplicatesAndLimitsCollection()
    {
        var aliases = Enumerable.Range(0, 12)
            .Select(index => $"alias{index}")
            .Append("ALIAS1");

        var normalized = WakeWordAliasPolicy.NormalizeMany(aliases);

        Assert.Equal(WakeWordAliasPolicy.MaximumAliases, normalized.Count);
        Assert.Equal(normalized.Count, normalized.Distinct(StringComparer.Ordinal).Count());
    }
}
