using Nexo.Core.Assistant;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D58 — reconocer los bloques que una respuesta ya trae dentro.
///
/// Adler lo describió con una receta: el modelo escribe «Ingredientes» e «Instrucciones», pero
/// todo llegaba como un muro con asteriscos crudos alrededor de los títulos. La estructura estaba
/// en el texto y no en la pantalla.
/// </summary>
public sealed class AnswerSectionsTests
{
    [Fact]
    public void BoldLines_AreTitles()
    {
        // La forma exacta que él enseñó.
        var sections = AnswerSections.Parse(
            "**Ingredientes**\nHarina\nAgua\n\n**Instrucciones**\nMezclar\nHornear");

        Assert.Equal(2, sections.Count);
        Assert.Equal("Ingredientes", sections[0].Title);
        Assert.Equal("Harina\nAgua", sections[0].Body.Replace("\r\n", "\n"));
        Assert.Equal("Instrucciones", sections[1].Title);
    }

    [Fact]
    public void MarkdownHeadings_AreTitlesToo()
    {
        var sections = AnswerSections.Parse("## Ingredientes\nHarina\n\n### Pasos\nMezclar");

        Assert.Equal(2, sections.Count);
        Assert.Equal("Ingredientes", sections[0].Title);
        Assert.Equal("Pasos", sections[1].Title);
    }

    [Fact]
    public void TheOpeningParagraph_KeepsNoTitle()
    {
        var sections = AnswerSections.Parse(
            "Aquí tienes la receta.\n\n**Ingredientes**\nHarina\n\n**Pasos**\nMezclar");

        Assert.Equal(3, sections.Count);
        Assert.Equal(string.Empty, sections[0].Title);
        Assert.Equal("Aquí tienes la receta.", sections[0].Body);
    }

    [Fact]
    public void ATrailingColon_IsNotPartOfTheTitle() =>
        Assert.Equal(
            "Ingredientes",
            AnswerSections.Parse("**Ingredientes:**\nHarina\n\n**Pasos:**\nMezclar")[0].Title);

    [Fact]
    public void AShortAnswer_IsNotWorthBoxing()
    {
        // Meter dos frases en una caja con título las hace parecer más importantes y más largas de
        // lo que son: el fallo contrario al que se está arreglando.
        Assert.Empty(AnswerSections.Parse("Son las cuatro y media."));
    }

    [Fact]
    public void ASingleHeading_IsAParagraphWithATitle_NotAStructure() =>
        Assert.Empty(AnswerSections.Parse("Aquí va.\n\n**Ingredientes**\nHarina\nAgua"));

    [Fact]
    public void BoldInTheMiddleOfASentence_IsEmphasisNotATitle()
    {
        // Partir por aquí rompería la respuesta justo por donde no toca.
        Assert.Empty(AnswerSections.Parse(
            "Usa **harina de fuerza** y nada más.\nLuego amasa con **agua tibia**."));
    }

    [Fact]
    public void TwoBoldRunsOnOneLine_AreNotATitle() =>
        Assert.Empty(AnswerSections.Parse("**uno** y **dos**\ntexto\n\n**tres** y **cuatro**\nmás"));

    [Fact]
    public void AHashWithoutASpace_IsNotAHeading()
    {
        // «#1 de la lista» o una etiqueta no son encabezados de Markdown.
        Assert.Empty(AnswerSections.Parse("#1 pasa esto\ntexto\n\n#2 y esto\nmás texto"));
    }

    [Fact]
    public void EmptyOrBlank_YieldsNothing()
    {
        Assert.Empty(AnswerSections.Parse(null));
        Assert.Empty(AnswerSections.Parse("   \n  "));
    }
}
