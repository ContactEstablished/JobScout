using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace JobScout.Infrastructure.Tests.TestHelpers;

/// <summary>
/// Builds in-memory resume documents for parser tests so we don't commit binary fixtures.
/// </summary>
public static class ResumeFixtures
{
    public static string TxtResumePath
        => Path.Combine(AppContext.BaseDirectory, "fixtures", "resumes", "sample.txt");

    public static MemoryStream BuildDocx(string text)
    {
        var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(
                new Body(
                    text.Split('\n').Select(line =>
                        new Paragraph(new Run(new Text(line)))).Cast<OpenXmlElement>().ToArray()));
            mainPart.Document.Save();
        }
        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// Minimal valid single-page PDF containing the given text.
    /// Built by hand because PdfPig parses, but does not author, PDFs.
    /// </summary>
    public static MemoryStream BuildPdf(string text)
    {
        var safe = text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        var contentStream = $"BT /F1 12 Tf 50 750 Td ({safe}) Tj ET";

        var objects = new List<string>
        {
            "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n",
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n",
            "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] " +
                "/Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>\nendobj\n",
            $"4 0 obj\n<< /Length {contentStream.Length} >>\nstream\n{contentStream}\nendstream\nendobj\n",
            "5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n"
        };

        var sb = new System.Text.StringBuilder();
        sb.Append("%PDF-1.4\n");

        var offsets = new List<long> { 0 };
        foreach (var obj in objects)
        {
            offsets.Add(sb.Length);
            sb.Append(obj);
        }

        var xrefOffset = sb.Length;
        sb.Append($"xref\n0 {objects.Count + 1}\n");
        sb.Append("0000000000 65535 f \n");
        foreach (var off in offsets.Skip(1))
            sb.Append($"{off:D10} 00000 n \n");

        sb.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\n");
        sb.Append($"startxref\n{xrefOffset}\n%%EOF");

        var bytes = System.Text.Encoding.ASCII.GetBytes(sb.ToString());
        return new MemoryStream(bytes);
    }
}
