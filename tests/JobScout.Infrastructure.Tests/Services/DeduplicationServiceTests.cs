using JobScout.Infrastructure.Services;

namespace JobScout.Infrastructure.Tests.Services;

public class DeduplicationServiceTests
{
    private readonly DeduplicationService _service =
        new(Substitute.For<IJobRepository>());

    [Theory]
    [InlineData("Sr. Software Developer (Remote)", "senior software developer")]
    [InlineData("Junior Backend Engineer", "junior backend engineer")]
    [InlineData("Software Developer", "Software Developer")]
    public void NormalizeTitle_EquivalentTitlesNormalizeEqual(string a, string b)
    {
        _service.NormalizeTitle(a).Should().Be(_service.NormalizeTitle(b));
    }

    [Fact]
    public void NormalizeTitle_DifferentRolesNormalizeDistinct()
    {
        _service.NormalizeTitle("Software Developer")
            .Should().NotBe(_service.NormalizeTitle("Data Scientist"));
    }

    [Theory]
    [InlineData("Acme Corp, Inc.", "ACME, Inc.")]
    [InlineData("Foo Bar LLC", "Foo Bar")]
    [InlineData("Globex Limited", "globex ltd")]
    public void NormalizeCompany_StripsLegalSuffixes(string a, string b)
    {
        _service.NormalizeCompany(a).Should().Be(_service.NormalizeCompany(b));
    }

    [Fact]
    public void NormalizeCompany_DistinctCompaniesNormalizeDifferently()
    {
        _service.NormalizeCompany("Acme Corp")
            .Should().NotBe(_service.NormalizeCompany("Globex"));
    }
}
