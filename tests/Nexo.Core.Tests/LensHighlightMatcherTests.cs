using Nexo.Core.Vision;

namespace Nexo.Core.Tests;

public sealed class LensHighlightMatcherTests
{
    private static readonly IReadOnlyList<UiAutomationElement> NoElements = [];

    [Fact]
    public void FindMatches_WithBlankAnswer_ReturnsNoRegions()
    {
        var ocr = OcrResult.Success("Guardar", [new OcrTextLine("Guardar", 10, 20, 50, 15)]);

        var regions = LensHighlightMatcher.FindMatches("   ", ocr, NoElements, 0, 0);

        Assert.Empty(regions);
    }

    [Fact]
    public void FindMatches_WithFailedOcr_ReturnsNoRegions()
    {
        var regions = LensHighlightMatcher.FindMatches(
            "Pulsa Guardar", OcrResult.Failed("motivo"), NoElements, 0, 0);

        Assert.Empty(regions);
    }

    [Fact]
    public void FindMatches_OcrLineMentionedInAnswer_IsHighlighted_WithWindowOffsetApplied()
    {
        var ocr = OcrResult.Success(
            "Guardar cambios", [new OcrTextLine("Guardar cambios", 10, 20, 80, 15)]);

        var regions = LensHighlightMatcher.FindMatches(
            "Para terminar, pulsa el botón Guardar cambios.", ocr, NoElements, windowLeft: 100, windowTop: 200);

        var region = Assert.Single(regions);
        Assert.Equal(110, region.Left);
        Assert.Equal(220, region.Top);
        Assert.Equal(80, region.Width);
        Assert.Equal(15, region.Height);
    }

    [Fact]
    public void FindMatches_OcrLineNotMentioned_IsNotHighlighted()
    {
        var ocr = OcrResult.Success(
            "Cancelar", [new OcrTextLine("Cancelar", 0, 0, 40, 15)]);

        var regions = LensHighlightMatcher.FindMatches(
            "Pulsa Guardar cambios para continuar.", ocr, NoElements, 0, 0);

        Assert.Empty(regions);
    }

    [Fact]
    public void FindMatches_IsCaseAndAccentInsensitive()
    {
        var ocr = OcrResult.Success(
            "Configuración", [new OcrTextLine("Configuración", 0, 0, 60, 15)]);

        var regions = LensHighlightMatcher.FindMatches(
            "Ve a CONFIGURACION para cambiar esto.", ocr, NoElements, 0, 0);

        Assert.Single(regions);
    }

    [Fact]
    public void FindMatches_ShortOcrLine_IsNeverHighlightedEvenIfMentioned()
    {
        var ocr = OcrResult.Success("OK", [new OcrTextLine("OK", 0, 0, 20, 15)]);

        var regions = LensHighlightMatcher.FindMatches("Pulsa OK para confirmar.", ocr, NoElements, 0, 0);

        Assert.Empty(regions);
    }

    [Fact]
    public void FindMatches_UiElementMentioned_IsHighlighted_WithoutWindowOffset()
    {
        var ocr = OcrResult.Success(string.Empty, []);
        var elements = new[] { new UiAutomationElement("Enviar formulario", "ControlType.Button", 500, 600, 90, 30) };

        var regions = LensHighlightMatcher.FindMatches(
            "Da clic en Enviar formulario.", ocr, elements, windowLeft: 100, windowTop: 200);

        var region = Assert.Single(regions);
        // Las coordenadas de UI Automation ya son absolutas: no se les suma windowLeft/windowTop.
        Assert.Equal(500, region.Left);
        Assert.Equal(600, region.Top);
    }

    [Fact]
    public void FindMatches_RedactedPlaceholder_NeverMatchesEvenIfPresentInAnswer()
    {
        var ocr = OcrResult.Success(
            "[REDACTADO]", [new OcrTextLine("[REDACTADO]", 0, 0, 60, 15)]);

        var regions = LensHighlightMatcher.FindMatches(
            "El campo dice [REDACTADO] y no debería mostrarse.", ocr, NoElements, 0, 0);

        Assert.Empty(regions);
    }

    [Fact]
    public void FindMatches_DuplicateRegions_AreDeduplicated()
    {
        var ocr = OcrResult.Success(
            "Guardar cambios",
            [
                new OcrTextLine("Guardar cambios", 10, 20, 80, 15),
                new OcrTextLine("Guardar cambios", 10, 20, 80, 15)
            ]);

        var regions = LensHighlightMatcher.FindMatches(
            "Pulsa Guardar cambios.", ocr, NoElements, 0, 0);

        Assert.Single(regions);
    }

    [Fact]
    public void FindMatches_ElementWithNullName_IsNeverHighlighted()
    {
        var ocr = OcrResult.Success(string.Empty, []);
        var elements = new[] { new UiAutomationElement(null, "ControlType.Pane", 0, 0, 50, 50) };

        var regions = LensHighlightMatcher.FindMatches("Cualquier respuesta.", ocr, elements, 0, 0);

        Assert.Empty(regions);
    }
}
