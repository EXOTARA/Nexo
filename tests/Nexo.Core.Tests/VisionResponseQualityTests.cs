using Nexo.Core.Ai;

namespace Nexo.Core.Tests;

public sealed class VisionResponseQualityTests
{
    private static readonly VisionDiagnosticEvidence Evidence = new()
    {
        ErrorVisible = true,
        ErrorCode = "CS0266",
        FileName = "WindowsScreenCaptureService.cs",
        LineNumber = 250,
        VisibleMessage = "No se puede convertir uint en nint"
    };

    [Fact]
    public void IsTooGeneric_ReturnsTrueForSupportDeflection()
    {
        Assert.True(VisionResponseQuality.IsTooGeneric(
            "Contacta al soporte técnico para resolverlo.",
            Evidence));
    }

    [Fact]
    public void IsTooGeneric_ReturnsTrueForShortVagueAnswer()
    {
        Assert.True(VisionResponseQuality.IsTooGeneric(
            "Hay un problema de tipos.",
            Evidence));
    }

    [Fact]
    public void IsTooGeneric_ReturnsFalseForConcreteCorrection()
    {
        const string response =
            "El error CS0266 está en WindowsScreenCaptureService.cs, línea 250. " +
            "Quita la asignación y vuelve a ejecutar dotnet build .\\Nexo.slnx.";

        Assert.False(VisionResponseQuality.IsTooGeneric(response, Evidence));
    }

    [Fact]
    public void IsTooGeneric_ReturnsFalseWhenNoErrorWasVisible()
    {
        var noError = new VisionDiagnosticEvidence
        {
            ErrorVisible = false,
            MissingInformation = "el mensaje completo"
        };

        Assert.False(VisionResponseQuality.IsTooGeneric(
            "No hay un error legible. Captura el mensaje completo.",
            noError));
    }
}
