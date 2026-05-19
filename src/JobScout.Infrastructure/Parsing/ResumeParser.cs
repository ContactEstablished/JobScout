using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using JobScout.Core.DTOs;
using UglyToad.PdfPig;

namespace JobScout.Infrastructure.Parsing;

public static class ResumeParser
{
    public static async Task<ResumeParseResult> ParseAsync(Stream stream, string extension)
    {
        var text = extension switch
        {
            ".txt"  => await ReadTxtAsync(stream),
            ".docx" => ReadDocx(stream),
            ".pdf"  => ReadPdf(stream),
            _       => string.Empty
        };

        var wordCount     = CountWords(text);
        var detectedSkills = ScanSkills(text);

        return new ResumeParseResult(text, wordCount, detectedSkills);
    }

    private static async Task<string> ReadTxtAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private static string ReadDocx(Stream stream)
    {
        using var doc = WordprocessingDocument.Open(stream, isEditable: false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return string.Empty;

        var sb = new StringBuilder();
        foreach (var para in body.Descendants<Paragraph>())
        {
            var line = para.InnerText.Trim();
            if (line.Length > 0)
                sb.AppendLine(line);
        }

        return sb.ToString();
    }

    private static string ReadPdf(Stream stream)
    {
        // PdfPig requires a seekable stream; copy to memory if needed
        Stream seekable = stream.CanSeek ? stream : CopyToMemory(stream);

        using var pdf = PdfDocument.Open(seekable);
        var sb = new StringBuilder();

        foreach (var page in pdf.GetPages())
        {
            var words = page.GetWords();
            sb.AppendLine(string.Join(" ", words.Select(w => w.Text)));
        }

        return sb.ToString();
    }

    private static MemoryStream CopyToMemory(Stream source)
    {
        var ms = new MemoryStream();
        source.CopyTo(ms);
        ms.Position = 0;
        return ms;
    }

    private static int CountWords(string text)
        => string.IsNullOrWhiteSpace(text)
            ? 0
            : text.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries).Length;

    private static string[] ScanSkills(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var found = new List<string>();
        foreach (var term in SkillsDictionary.Terms)
        {
            if (text.Contains(term, StringComparison.OrdinalIgnoreCase))
                found.Add(term);
        }

        return [.. found];
    }
}
