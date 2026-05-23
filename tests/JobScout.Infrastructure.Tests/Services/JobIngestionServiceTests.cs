using JobScout.Core.Enums;
using JobScout.Infrastructure.Repositories;
using JobScout.Infrastructure.Services;
using JobScout.Infrastructure.Tests.Builders;
using JobScout.Infrastructure.Tests.Fixtures;
using JobScout.Infrastructure.Tests.TestHelpers;

namespace JobScout.Infrastructure.Tests.Services;

public class JobIngestionServiceTests
{
    private static (JobIngestionService Service, INotificationService Notif) Build(
        JobScoutDbContext db, IEnumerable<StubJobBoardClient> stubs)
    {
        var jobRepo = new JobRepository(db);
        var dedup = new DeduplicationService(jobRepo);
        var notif = Substitute.For<INotificationService>();
        return (new JobIngestionService(stubs, jobRepo, dedup, notif, NullLogger<JobIngestionService>.Instance), notif);
    }

    [Fact]
    public async Task IngestAsync_SkipsExactDuplicates_OnSameExternalIdAndSource()
    {
        using var fx = new SqliteFixture();
        await fx.SeedUserAsync();
        await using var db = fx.CreateContext();

        var profile = new ProfileBuilder().Build();
        db.SearchProfiles.Add(profile);
        await db.SaveChangesAsync();

        var dup = new JobBuilder().WithExternalId("EXT-1").WithSource(JobSource.RemoteOK).Build();
        var stub = new StubJobBoardClient(JobSource.RemoteOK, [dup, dup]); // emit same twice

        var (service, _) = Build(db, [stub]);
        var result = await service.IngestAsync(profile);

        result.NewJobsFound.Should().Be(1);
        result.Duplicates.Should().Be(1);
    }

    [Fact]
    public async Task IngestAsync_FlagsFuzzyDuplicateAcrossDifferentSources()
    {
        using var fx = new SqliteFixture();
        await fx.SeedUserAsync();
        await using var db = fx.CreateContext();

        var profile = new ProfileBuilder().Build();
        db.SearchProfiles.Add(profile);
        await db.SaveChangesAsync();

        // Production fuzzy match runs two normalizations:
        //   1) DeduplicationService strips stopwords + suffixes ("acme" / "software developer")
        //   2) JobRepository compares full-text minus case/punctuation
        // Titles/companies must therefore match under BOTH, which means no stopwords differing.
        var indeedJob = new JobBuilder()
            .WithExternalId("indeed-1")
            .WithTitle("Software Developer")
            .WithCompany("Acme")
            .WithSource(JobSource.Indeed)
            .Build();
        var linkedInJob = new JobBuilder()
            .WithExternalId("li-1")
            .WithTitle("software developer")
            .WithCompany("ACME")
            .WithSource(JobSource.LinkedIn)
            .Build();

        // First ingest seeds the Indeed job; second ingest brings in LinkedIn — guarantees order.
        var stubIndeed = new StubJobBoardClient(JobSource.Indeed, [indeedJob]);
        var (service1, _) = Build(db, [stubIndeed]);
        await service1.IngestAsync(profile);

        var stubLinkedIn = new StubJobBoardClient(JobSource.LinkedIn, [linkedInJob]);
        var (service2, _) = Build(db, [stubLinkedIn]);
        var result = await service2.IngestAsync(profile);

        result.NewJobsFound.Should().Be(1);
        result.FuzzyDuplicates.Should().Be(1);

        var stored = await db.Jobs.OrderBy(j => j.DiscoveredAt).ToListAsync();
        stored.Should().HaveCount(2);
        stored[1].IsPotentialDuplicate.Should().BeTrue();
        stored[1].DuplicateOfJobId.Should().Be(stored[0].Id);
    }

    [Fact]
    public async Task IngestAsync_RespectsProfilePreferredSources()
    {
        using var fx = new SqliteFixture();
        await fx.SeedUserAsync();
        await using var db = fx.CreateContext();

        var profile = new ProfileBuilder().WithPreferredSources(JobSource.RemoteOK).Build();
        db.SearchProfiles.Add(profile);
        await db.SaveChangesAsync();

        var remoteStub = new StubJobBoardClient(JobSource.RemoteOK,
            [new JobBuilder().WithSource(JobSource.RemoteOK).WithExternalId("r-1").Build()]);
        var adzunaStub = new StubJobBoardClient(JobSource.Adzuna,
            [new JobBuilder().WithSource(JobSource.Adzuna).WithExternalId("a-1").Build()]);

        var (service, _) = Build(db, [remoteStub, adzunaStub]);
        await service.IngestAsync(profile);

        remoteStub.CallCount.Should().Be(1);
        adzunaStub.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task IngestAsync_CompletesWhenOneSourceThrows()
    {
        using var fx = new SqliteFixture();
        await fx.SeedUserAsync();
        await using var db = fx.CreateContext();

        var profile = new ProfileBuilder().Build();
        db.SearchProfiles.Add(profile);
        await db.SaveChangesAsync();

        var ok1 = new StubJobBoardClient(JobSource.RemoteOK,
            [new JobBuilder().WithSource(JobSource.RemoteOK).WithExternalId("r-1").Build()]);
        var ok2 = new StubJobBoardClient(JobSource.Adzuna,
            [new JobBuilder().WithSource(JobSource.Adzuna).WithExternalId("a-1").Build()]);
        var broken = new StubJobBoardClient(JobSource.Indeed, [], new HttpRequestException("503"));

        var (service, _) = Build(db, [ok1, ok2, broken]);
        var result = await service.IngestAsync(profile);

        result.NewJobsFound.Should().Be(2);
    }

    [Fact]
    public async Task IngestAsync_FiresNotification_WhenAtLeastOneNewJob()
    {
        using var fx = new SqliteFixture();
        await fx.SeedUserAsync();
        await using var db = fx.CreateContext();

        var profile = new ProfileBuilder().Build();
        db.SearchProfiles.Add(profile);
        await db.SaveChangesAsync();

        var stub = new StubJobBoardClient(JobSource.RemoteOK,
            [new JobBuilder().WithSource(JobSource.RemoteOK).WithExternalId("r-1").Build()]);

        var (service, notif) = Build(db, [stub]);
        await service.IngestAsync(profile);

        await notif.Received(1).OnIngestionCompleteAsync(
            Arg.Is<SearchProfile>(p => p.Id == profile.Id),
            Arg.Is<IngestionResult>(r => r.NewJobsFound == 1),
            Arg.Any<CancellationToken>());
    }

}
