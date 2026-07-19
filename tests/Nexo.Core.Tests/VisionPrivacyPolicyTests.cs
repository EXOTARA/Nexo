using Nexo.Core.Vision;

namespace Nexo.Core.Tests;

public sealed class VisionPrivacyPolicyTests
{
    [Theory]
    [InlineData("Bitwarden", "Bitwarden")]
    [InlineData("Administrador de credenciales", "CredentialUIBroker")]
    [InlineData("Seguridad de Windows", "SecurityHealthHost")]
    [InlineData("KeePassXC", "keepassxc")]
    public void IsSensitive_BlocksCredentialWindows(string title, string process)
    {
        Assert.True(VisionPrivacyPolicy.IsSensitive(title, process));
    }

    [Theory]
    [InlineData("Visual Studio Code", "Code")]
    [InlineData("Nexo - GitHub", "zen")]
    [InlineData("Spotify", "Spotify")]
    public void IsSensitive_AllowsNormalApplications(string title, string process)
    {
        Assert.False(VisionPrivacyPolicy.IsSensitive(title, process));
    }

    [Fact]
    public void IsSensitive_UsesCustomExclusions()
    {
        Assert.True(VisionPrivacyPolicy.IsSensitive(
            "Mi banca personal",
            "zen",
            ["banca"]));
    }
}
