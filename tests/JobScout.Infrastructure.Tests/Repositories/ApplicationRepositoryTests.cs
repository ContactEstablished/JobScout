using JobScout.Core.Enums;
using JobScout.Infrastructure.Repositories;
using JobScout.Infrastructure.Tests.Builders;
using JobScout.Infrastructure.Tests.Fixtures;

namespace JobScout.Infrastructure.Tests.Repositories;

public class ApplicationRepositoryTests
{
    [Fact]
    public async Task GetPipelineAsync_ReturnsCorrectCountsPerStatus()
    {
        using var fx = new SqliteFixture();
        await fx.SeedUserAsync("user-1");
        await using var db = fx.CreateContext();

        var profile = new ProfileBuilder().WithUserId("user-1").Build();
        db.SearchProfiles.Add(profile);

        var jobs = Enumerable.Range(0, 5).Select(_ => new JobBuilder().Build()).ToList();
        db.Jobs.AddRange(jobs);

        db.JobApplications.AddRange(
            new JobApplicationBuilder().ForJob(jobs[0].Id).ForProfile(profile.Id).WithStatus(ApplicationStatus.Applied).Build(),
            new JobApplicationBuilder().ForJob(jobs[1].Id).ForProfile(profile.Id).WithStatus(ApplicationStatus.Applied).Build(),
            new JobApplicationBuilder().ForJob(jobs[2].Id).ForProfile(profile.Id).WithStatus(ApplicationStatus.Applied).Build(),
            new JobApplicationBuilder().ForJob(jobs[3].Id).ForProfile(profile.Id).WithStatus(ApplicationStatus.Interviewing).Build(),
            new JobApplicationBuilder().ForJob(jobs[4].Id).ForProfile(profile.Id).WithStatus(ApplicationStatus.Interviewing).Build());

        await db.SaveChangesAsync();

        var repo = new ApplicationRepository(db);
        var pipeline = await repo.GetPipelineAsync(profile.Id, "user-1");

        pipeline.Applied.Should().Be(3);
        pipeline.Interviewing.Should().Be(2);
        pipeline.Offered.Should().Be(0);
        pipeline.Rejected.Should().Be(0);
    }

    [Fact]
    public async Task GetByIdAsync_IsScopedToUser()
    {
        using var fx = new SqliteFixture();
        await fx.SeedUserAsync("user-a");
        await fx.SeedUserAsync("user-b");
        await using var db = fx.CreateContext();

        var bProfile = new ProfileBuilder().WithUserId("user-b").Build();
        var job = new JobBuilder().Build();
        db.SearchProfiles.Add(bProfile);
        db.Jobs.Add(job);
        var app = new JobApplicationBuilder().ForJob(job.Id).ForProfile(bProfile.Id).Build();
        db.JobApplications.Add(app);
        await db.SaveChangesAsync();

        var repo = new ApplicationRepository(db);
        var leak = await repo.GetByIdAsync(app.Id, "user-a");
        leak.Should().BeNull();

        var owner = await repo.GetByIdAsync(app.Id, "user-b");
        owner.Should().NotBeNull();
    }
}
