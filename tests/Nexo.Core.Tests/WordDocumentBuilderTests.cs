using System.IO.Compression;
using System.Xml.Linq;
using Nexo.Core.Assistant;
using Nexo.Core.Documents;

namespace Nexo.Core.Tests;

/// <summary>
/// Diseño D59 — el .docx que Sakura deja en el escritorio.
///
/// Lo que estas pruebas protegen no es el formato en abstracto: es que el archivo **abra**. Un
/// .docx al que le falta una pieza o que lleva un carácter que XML no admite no se abre a medias,
/// Word lo declara dañado y se niega entero — y eso pasaría al final de un trabajo largo, que es el
/// peor momento para descubrirlo.
/// </summary>
public sealed class WordDocumentBuilderTests
{
    private static readonly AnswerSection[] Recipe =
    [
        new("Ingredientes", "Harina\nAgua"),
        new("Instrucciones", "Mezclar\nHornear")
    ];

    private static ZipArchive Open(byte[] document) =>
        new(new MemoryStream(document), ZipArchiveMode.Read);

    private static string ReadPart(byte[] document, string path)
    {
        using var archive = Open(document);
        var entry = archive.GetEntry(path);
        Assert.NotNull(entry);

        using var reader = new StreamReader(entry!.Open());
        return reader.ReadToEnd();
    }

    [Fact]
    public void EveryPartWordNeedsIsPresent()
    {
        // Falta una y Word no abre nada. No hay degradación elegante posible aquí.
        using var archive = Open(WordDocumentBuilder.Build("Receta", Recipe));

        var parts = archive.Entries.Select(entry => entry.FullName).ToList();

        Assert.Contains("[Content_Types].xml", parts);
        Assert.Contains("_rels/.rels", parts);
        Assert.Contains("word/document.xml", parts);
        Assert.Contains("word/_rels/document.xml.rels", parts);
        Assert.Contains("word/styles.xml", parts);
    }

    [Fact]
    public void EveryPartIsWellFormedXml()
    {
        var document = WordDocumentBuilder.Build("Receta", Recipe);

        foreach (var part in new[]
                 {
                     "[Content_Types].xml", "_rels/.rels",
                     "word/document.xml", "word/_rels/document.xml.rels", "word/styles.xml"
                 })
        {
            var exception = Record.Exception(() => XDocument.Parse(ReadPart(document, part)));
            Assert.Null(exception);
        }
    }

    [Fact]
    public void SectionTitlesBecomeRealHeadings()
    {
        // Un Heading1 de verdad es lo que hace que Word construya el panel de navegación. Imitar el
        // aspecto con negrita daría un documento que solo parece tener apartados.
        var xml = ReadPart(WordDocumentBuilder.Build("Receta", Recipe), "word/document.xml");

        Assert.Contains("w:val=\"Heading1\"", xml, StringComparison.Ordinal);
        Assert.Contains("Ingredientes", xml, StringComparison.Ordinal);
        Assert.Contains("Instrucciones", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void EachLineBecomesItsOwnParagraph()
    {
        // Volcar el bloque entero de una vez dejaría los pasos pegados en un ladrillo.
        var xml = ReadPart(WordDocumentBuilder.Build(string.Empty, Recipe), "word/document.xml");

        foreach (var line in new[] { "Harina", "Agua", "Mezclar", "Hornear" })
        {
            Assert.Contains(">" + line + "</w:t>", xml, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MarkupInTheAnswerIsEscaped_NotInjected()
    {
        // Un modelo que responde sobre HTML escribe etiquetas. Sin escapar, el documento deja de ser
        // XML válido y Word lo rechaza entero.
        var document = WordDocumentBuilder.Build(
            "Prueba", [new AnswerSection("Código", "<w:p> & </w:p>")]);

        var xml = ReadPart(document, "word/document.xml");

        Assert.Contains("&lt;w:p&gt; &amp; &lt;/w:p&gt;", xml, StringComparison.Ordinal);
        Assert.Null(Record.Exception(() => XDocument.Parse(xml)));
    }

    [Fact]
    public void ControlCharactersAreDropped_RatherThanBreakingTheFile()
    {
        // Basta uno para que Word declare el archivo dañado. Perder un carácter invisible es mejor
        // que perder el documento.
        var document = WordDocumentBuilder.Build(
            "Prueba", [new AnswerSection("Roto", "holamundo")]);

        var xml = ReadPart(document, "word/document.xml");

        Assert.Contains("holamundo", xml, StringComparison.Ordinal);
        Assert.Null(Record.Exception(() => XDocument.Parse(xml)));
    }

    [Fact]
    public void ATitleIsOptional()
    {
        var xml = ReadPart(WordDocumentBuilder.Build(string.Empty, Recipe), "word/document.xml");

        Assert.DoesNotContain("w:val=\"Title\"", xml, StringComparison.Ordinal);
        Assert.Null(Record.Exception(() => XDocument.Parse(xml)));
    }

    [Fact]
    public void AnEmptyAnswerStillProducesAFileThatOpens()
    {
        var document = WordDocumentBuilder.Build("Vacío", []);

        Assert.NotEmpty(document);
        Assert.Null(Record.Exception(() => XDocument.Parse(ReadPart(document, "word/document.xml"))));
    }

    [Fact]
    public void IndentationSurvives()
    {
        // Sin xml:space="preserve" Word recorta los espacios iniciales y una lista sangrada acaba
        // pegada al margen.
        var xml = ReadPart(
            WordDocumentBuilder.Build(string.Empty, [new AnswerSection("Pasos", "    sangrado")]),
            "word/document.xml");

        Assert.Contains("xml:space=\"preserve\"", xml, StringComparison.Ordinal);
        Assert.Contains("    sangrado", xml, StringComparison.Ordinal);
    }
}
