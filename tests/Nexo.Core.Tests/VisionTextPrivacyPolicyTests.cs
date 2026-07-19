using Nexo.Core.Vision;

namespace Nexo.Core.Tests;

public sealed class VisionTextPrivacyPolicyTests
{
    [Fact]
    public void Analyze_DetectsEmailAndPossibleSecret()
    {
        const string text = "Correo: user@example.com Token: sk-abcdefghijklmnop";

        var findings = VisionTextPrivacyPolicy.Analyze(text);

        Assert.Contains(findings, finding => finding.Kind == "email");
        Assert.Contains(findings, finding => finding.Kind == "api-key");
    }

    [Fact]
    public void Analyze_ReturnsEmptyForNormalTechnicalText()
    {
        var findings = VisionTextPrivacyPolicy.Analyze(
            "CS0266 en WindowsScreenCaptureService.cs línea 250");

        Assert.Empty(findings);
    }

    [Fact]
    public void ParseCustomExclusions_NormalizesAndRemovesDuplicates()
    {
        var exclusions = VisionTextPrivacyPolicy.ParseCustomExclusions(
            "Banco, correo privado;Banco\nKeePass");

        Assert.Equal(3, exclusions.Count);
        Assert.Contains("Banco", exclusions);
        Assert.Contains("correo privado", exclusions);
        Assert.Contains("KeePass", exclusions);
    }
}
