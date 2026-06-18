using JobScout.Infrastructure.Repositories;
using JobScout.Infrastructure.Tests.Builders;
using JobScout.Infrastructure.Tests.Fixtures;

namespace JobScout.Infrastructure.Tests.Repositories;

public class JobRepositoryTests
{
    private static async Task<Guid> SeedProfileAsync(SqliteFixture fx, string userId = "user-1")
    {
        await fx.SeedUserAsync(userId);
        await using var db = fx.CreateContext();
        var profile = new ProfileBuilder().WithUserId(userId).Build();
        db.SearchProfiles.Add(profile);
        await db.SaveChangesAsync();
        return profile.Id;
    }

    private static async Task AddScoredJobAsync(
        SqliteFixture fx, Guid profileId, Job job, decimal score, string? reasoning = null)
    {
        await using var db = fx.CreateContext();
        db.Jobs.Add(job);
        var builder = new AiScoreBuilder().ForJob(job.Id).ForProfile(profileId).WithScore(score);
        if (reasoning is not null) builder = builder.WithReasoning(reasoning);
        db.AiScores.Add(builder.Build());
        await db.SaveChangesAsync();
    }

    // ---- 9.4.a full-text search ----

    [Fact]
    public async Task Search_MatchesDescription_NotJustTitleOrCompany()
    {
        using var fx = new SqliteFixture();
        var profileId = await SeedProfileAsync(fx);

        var match = new JobBuilder().WithTitle("Backend Engineer").WithCompany("Acme")
            .WithDescription("We run Kubernetes across three regions.").Build();
        var noMatch = new JobBuilder().WithTitle("Frontend Engineer").WithCompany("Globex")
            .WithDescription("React, TypeScript, CSS.").Build();
        await AddScoredJobAsync(fx, profileId, match, 7m);
        await AddScoredJobAsync(fx, profileId, noMatch, 7m);

        await using var db = fx.CreateContext();
        var (items, total) = await new JobRepository(db)
            .GetByProfileAsync(profileId, new JobFilterOptions { Query = "kubernetes" }, 1, 20);

        total.Should().Be(1);
        items.Should().ContainSingle().Which.Id.Should().Be(match.Id);
    }

    [Fact]
    public async Task Search_MatchesAiReasoning()
    {
        using var fx = new SqliteFixture();
        var profileId = await SeedProfileAsync(fx);

        var job = new JobBuilder().WithTitle("Engineer").WithCompany("Acme")
            .WithDescription("A generic role.").Build();
        await AddScoredJobAsync(fx, profileId, job, 8m,
            reasoning: "Strong match on the candidate's distributed-systems background.");

        await using var db = fx.CreateContext();
        var (items, total) = await new JobRepository(db)
            .GetByProfileAsync(profileId, new JobFilterOptions { Query = "distributed" }, 1, 20);

        total.Should().Be(1);
        items.Single().Id.Should().Be(job.Id);
    }

    [Fact]
    public async Task Search_IsCaseInsensitive_AndComposesWithSourceFilter()
    {
        using var fx = new SqliteFixture();
        var profileId = await SeedProfileAsync(fx);

        var remote = new JobBuilder().WithTitle("Azure Architect").WithSource(JobSource.RemoteOK).Build();
        var adzuna = new JobBuilder().WithTitle("Azure Architect").WithSource(JobSource.Adzuna).Build();
        await AddScoredJobAsync(fx, profileId, remote, 7m);
        await AddScoredJobAsync(fx, profileId, adzuna, 7m);

        await using var db = fx.CreateContext();
        var (items, total) = await new JobRepository(db).GetByProfileAsync(
            profileId, new JobFilterOptions { Query = "AZURE", Source = JobSource.RemoteOK }, 1, 20);

        total.Should().Be(1);
        items.Single().Source.Should().Be(JobSource.RemoteOK);
    }

    // ---- 9.4.b sort ----

    [Fact]
    public async Task Sort_ByAiScore_IsDefault_AndOrdersDescending()
    {
        using var fx = new SqliteFixture();
        var profileId = await SeedProfileAsync(fx);

        await AddScoredJobAsync(fx, profileId, new JobBuilder().WithTitle("Low").Build(), 5.0m);
        await AddScoredJobAsync(fx, profileId, new JobBuilder().WithTitle("High").Build(), 9.5m);
        await AddScoredJobAsync(fx, profileId, new JobBuilder().WithTitle("Mid").Build(), 7.0m);

        await using var db = fx.CreateContext();
        // No SortBy set → defaults to AiScore.
        var (items, _) = await new JobRepository(db)
            .GetByProfileAsync(profileId, new JobFilterOptions(), 1, 20);

        items.Select(j => j.Title).Should().ContainInOrder("High", "Mid", "Low");
    }

    [Fact]
    public async Task Sort_ByPostedDate_OrdersNewestFirst()
    {
        using var fx = new SqliteFixture();
        var profileId = await SeedProfileAsync(fx);

        var older = new JobBuilder().WithTitle("Older").WithPostedAt(DateTime.UtcNow.AddDays(-10)).Build();
        var newer = new JobBuilder().WithTitle("Newer").WithPostedAt(DateTime.UtcNow.AddDays(-1)).Build();
        await AddScoredJobAsync(fx, profileId, older, 9m);   // higher score but older
        await AddScoredJobAsync(fx, profileId, newer, 6m);

        await using var db = fx.CreateContext();
        var (items, _) = await new JobRepository(db)
            .GetByProfileAsync(profileId, new JobFilterOptions { SortBy = JobSortBy.PostedDate }, 1, 20);

        items.Select(j => j.Title).Should().ContainInOrder("Newer", "Older");
    }

    [Fact]
    public async Task Sort_ByCompany_OrdersAlphabetically()
    {
        using var fx = new SqliteFixture();
        var profileId = await SeedProfileAsync(fx);

        await AddScoredJobAsync(fx, profileId, new JobBuilder().WithCompany("Zeta").Build(), 7m);
        await AddScoredJobAsync(fx, profileId, new JobBuilder().WithCompany("Alpha").Build(), 7m);
        await AddScoredJobAsync(fx, profileId, new JobBuilder().WithCompany("Mango").Build(), 7m);

        await using var db = fx.CreateContext();
        var (items, _) = await new JobRepository(db)
            .GetByProfileAsync(profileId, new JobFilterOptions { SortBy = JobSortBy.Company }, 1, 20);

        items.Select(j => j.Company).Should().ContainInOrder("Alpha", "Mango", "Zeta");
    }

    // ---- existing guarantees still hold ----

    [Fact]
    public async Task GetByProfile_ExcludesInactiveAndUnscoredJobs()
    {
        using var fx = new SqliteFixture();
        var profileId = await SeedProfileAsync(fx);

        var active = new JobBuilder().WithTitle("Active").Build();
        await AddScoredJobAsync(fx, profileId, active, 7m);

        var inactive = new JobBuilder().WithTitle("Inactive").InactiveJob().Build();
        await AddScoredJobAsync(fx, profileId, inactive, 7m);

        await using (var seed = fx.CreateContext())
        {
            seed.Jobs.Add(new JobBuilder().WithTitle("Unscored").Build()); // no AiScore for this profile
            await seed.SaveChangesAsync();
        }

        await using var db = fx.CreateContext();
        var (items, total) = await new JobRepository(db).GetByProfileAsync(profileId, null, 1, 20);

        total.Should().Be(1);
        items.Single().Title.Should().Be("Active");
    }
}
