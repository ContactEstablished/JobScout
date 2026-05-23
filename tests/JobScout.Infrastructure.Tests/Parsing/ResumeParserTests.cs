using System.Text;
using JobScout.Infrastructure.Parsing;
using JobScout.Infrastructure.Tests.TestHelpers;

namespace JobScout.Infrastructure.Tests.Parsing;

public class ResumeParserTests
{
    [Fact]
    public async Task ParseAsync_Txt_RoundTripsContentAndDetectsSkills()
    {
        await using var stream = File.OpenRead(ResumeFixtures.TxtResumePath);
        var result = await ResumeParser.ParseAsync(stream, ".txt");

        result.PlainText.Should().Contain("Senior Software Engineer");
        result.WordCount.Should().BeGreaterThan(40);
        result.DetectedSkills.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ParseAsync_Txt_DetectsMultipleKnownSkills()
    {
        await using var stream = File.OpenRead(ResumeFixtures.TxtResumePath);
        var result = await ResumeParser.ParseAsync(stream, ".txt");

        var skills = result.DetectedSkills.Select(s => s.ToLowerInvariant()).ToHashSet();
        skills.Should().Contain("python");
        skills.Should().Contain("react");
        skills.Should().Contain("aws");
    }

    [Fact]
    public async Task ParseAsync_Docx_ExtractsText()
    {
        using var docx = ResumeFixtures.BuildDocx("Experience\nC# developer with React expertise.");
        var result = await ResumeParser.ParseAsync(docx, ".docx");

        result.PlainText.Should().Contain("C#");
        result.PlainText.Should().Contain("React");
        result.DetectedSkills.Should().Contain(s => s.Equals("react", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ParseAsync_Pdf_ExtractsWordsWithoutThrowing()
    {
        using var pdf = ResumeFixtures.BuildPdf("Hello world from a PDF");
        var result = await ResumeParser.ParseAsync(pdf, ".pdf");

        result.WordCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ParseAsync_EmptyStream_ReturnsEmptyResult()
    {
        await using var stream = new MemoryStream(Array.Empty<byte>());
        var result = await ResumeParser.ParseAsync(stream, ".txt");

        result.PlainText.Should().BeEmpty();
        result.WordCount.Should().Be(0);
        result.DetectedSkills.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_UnsupportedExtension_ReturnsEmpty()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("ignored"));
        var result = await ResumeParser.ParseAsync(stream, ".rtf");

        result.PlainText.Should().BeEmpty();
        result.WordCount.Should().Be(0);
        result.DetectedSkills.Should().BeEmpty();
    }
}
