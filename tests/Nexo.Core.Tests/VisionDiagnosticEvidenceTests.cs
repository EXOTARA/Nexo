using Nexo.Core.Ai;

namespace Nexo.Core.Tests;

public sealed class VisionDiagnosticEvidenceTests
{
    [Fact]
    public void Normalize_TrimsValuesAndClampsConfidence()
    {
        var evidence = new VisionDiagnosticEvidence
        {
            ErrorVisible = true,
            ErrorCode = " CS0266 ",
            FileName = " WindowsScreenCaptureService.cs ",
            LineNumber = -4,
            Confidence = 2.4
        };

        var normalized = evidence.Normalize();

        Assert.Equal("CS0266", normalized.ErrorCode);
        Assert.Equal("WindowsScreenCaptureService.cs", normalized.FileName);
        Assert.Null(normalized.LineNumber);
        Assert.Equal(1d, normalized.Confidence);
    }

    [Fact]
    public void BuildCompactSummary_IncludesCodeFileAndLine()
    {
        var evidence = new VisionDiagnosticEvidence
        {
            ErrorVisible = true,
            ErrorCode = "CS0266",
            FileName = "WindowsScreenCaptureService.cs",
            LineNumber = 250,
            VisibleMessage = "No se puede convertir uint en nint"
        };

        var summary = evidence.BuildCompactSummary();

        Assert.Contains("CS0266", summary);
        Assert.Contains("WindowsScreenCaptureService.cs", summary);
        Assert.Contains("250", summary);
        Assert.Contains("uint", summary);
    }

    [Fact]
    public void BuildCompactSummary_ExplainsWhenNoErrorIsVisible()
    {
        var evidence = new VisionDiagnosticEvidence
        {
            ErrorVisible = false,
            MissingInformation = "el mensaje completo"
        };

        Assert.Contains("el mensaje completo", evidence.BuildCompactSummary());
    }
}
