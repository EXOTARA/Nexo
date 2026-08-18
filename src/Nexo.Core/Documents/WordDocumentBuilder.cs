using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Nexo.Core.Assistant;

namespace Nexo.Core.Documents;

/// <summary>
/// Diseño D59 — escribe un .docx de verdad, sin depender de que Word esté instalado.
///
/// Un .docx es un ZIP con XML dentro (OOXML), así que se construye con lo que ya trae .NET. Se
/// descartó automatizar Word por COM, que era la vía obvia: exige tener Word, deja procesos
/// colgados cuando algo falla a medias, y falla de formas distintas según la versión. Kohana tiene
/// que poder dejarte el documento aunque no tengas Office — y si lo tienes, se abre igual.
///
/// El contenido entra como los mismos apartados con los que se enseña la respuesta en el chat
/// (<see cref="AnswerSection"/>), no como un texto plano que haya que volver a interpretar. Lo que
/// se ve en la conversación y lo que acaba en el documento salen de la misma lectura: si divergen,
/// uno de los dos está mintiendo sobre lo que dijo el modelo.
/// </summary>
public static class WordDocumentBuilder
{
    /// <summary>Construye el documento completo en memoria.</summary>
    public static byte[] Build(string title, IReadOnlyList<AnswerSection> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);

        using var buffer = new MemoryStream();

        // Sin leaveOpen, el ZIP cierra el flujo al liberarse y el array sale vacío.
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(archive, "[Content_Types].xml", ContentTypes);
            Write(archive, "_rels/.rels", RootRelationships);
            Write(archive, "word/_rels/document.xml.rels", DocumentRelationships);
            Write(archive, "word/styles.xml", Styles);
            Write(archive, "word/document.xml", BuildDocument(title, sections));
        }

        return buffer.ToArray();
    }

    private static void Write(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();

        // Sin BOM: Word lo tolera, pero algunos lectores estrictos lo tratan como contenido y se
        // caen al leer la declaración XML.
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string BuildDocument(string title, IReadOnlyList<AnswerSection> sections)
    {
        var body = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(title))
        {
            body.Append(Paragraph(title, "Title"));
        }

        foreach (var section in sections)
        {
            if (section.Title.Length > 0)
            {
                body.Append(Paragraph(section.Title, "Heading1"));
            }

            if (section.Body.Length == 0)
            {
                continue;
            }

            // Cada línea es un párrafo propio. Un salto de línea dentro de un párrafo de Word no
            // separa párrafos, así que volcar el bloque entero de una vez dejaría los pasos de una
            // receta pegados en un ladrillo, que es justo lo que se está arreglando.
            foreach (var line in section.Body.Replace("\r\n", "\n").Split('\n'))
            {
                body.Append(Paragraph(line, style: null));
            }
        }

        return XmlDeclaration +
            "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">" +
            "<w:body>" + body + SectionProperties + "</w:body></w:document>";
    }

    private static string Paragraph(string text, string? style)
    {
        var properties = style is null
            ? string.Empty
            : "<w:pPr><w:pStyle w:val=\"" + style + "\"/></w:pPr>";

        // xml:space="preserve" conserva la sangría de una lista o de un bloque de pasos; sin él
        // Word recorta los espacios iniciales y todo acaba pegado al margen.
        return "<w:p>" + properties +
               "<w:r><w:t xml:space=\"preserve\">" + Escape(text) + "</w:t></w:r></w:p>";
    }

    /// <summary>
    /// Escapa para XML y descarta lo que XML no admite.
    ///
    /// Un modelo puede devolver caracteres de control —restos de un token mal decodificado— y basta
    /// uno para que Word declare el archivo dañado y se niegue a abrirlo entero. Perder un carácter
    /// invisible es mejor que perder el documento.
    ///
    /// El ampersand va primero: si fuera después, convertiría en «&amp;lt;» los «&lt;» que acaban de
    /// escaparse.
    /// </summary>
    private static string Escape(string text)
    {
        var clean = new StringBuilder(text.Length);

        foreach (var character in text)
        {
            if (XmlConvert.IsXmlChar(character))
            {
                clean.Append(character);
            }
        }

        return clean.ToString()
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private const string XmlDeclaration =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>";

    private const string SectionProperties =
        "<w:sectPr><w:pgSz w:w=\"11906\" w:h=\"16838\"/>" +
        "<w:pgMar w:top=\"1134\" w:right=\"1134\" w:bottom=\"1134\" w:left=\"1134\"/></w:sectPr>";

    private const string ContentTypes =
        XmlDeclaration +
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
        "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
        "<Override PartName=\"/word/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml\"/>" +
        "</Types>";

    private const string RootRelationships =
        XmlDeclaration +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>" +
        "</Relationships>";

    private const string DocumentRelationships =
        XmlDeclaration +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
        "</Relationships>";

    /// <summary>
    /// Los dos estilos que se usan, declarados de verdad en vez de imitados con negrita y tamaño.
    /// Un Heading1 real es lo que hace que Word construya el panel de navegación y el índice
    /// automático: es la diferencia entre un documento con apartados y uno que solo lo parece.
    /// </summary>
    private const string Styles =
        XmlDeclaration +
        "<w:styles xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">" +
        "<w:style w:type=\"paragraph\" w:styleId=\"Title\"><w:name w:val=\"Title\"/>" +
        "<w:pPr><w:spacing w:after=\"240\"/></w:pPr>" +
        "<w:rPr><w:b/><w:sz w:val=\"52\"/></w:rPr></w:style>" +
        "<w:style w:type=\"paragraph\" w:styleId=\"Heading1\"><w:name w:val=\"heading 1\"/>" +
        "<w:pPr><w:outlineLvl w:val=\"0\"/><w:spacing w:before=\"280\" w:after=\"120\"/></w:pPr>" +
        "<w:rPr><w:b/><w:sz w:val=\"32\"/></w:rPr></w:style>" +
        "</w:styles>";
}
