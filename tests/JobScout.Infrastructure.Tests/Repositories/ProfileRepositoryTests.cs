using JobScout.Infrastructure.Repositories;
using JobScout.Infrastructure.Tests.Builders;
using JobScout.Infrastructure.Tests.Fixtures;

namespace JobScout.Infrastructure.Tests.Repositories;

public class ProfileRepositoryTests
{
    [Fact]
    public async Task AddAsync_ThenGetByIdAsync_RoundTripsPhase5Fields()
    {
        using var fx = new SqliteFixture();
        await fx.SeedUserAsync("user-1");
        await using var db = fx.CreateContext();
        var repo = new ProfileRepository(db);

        var profile = new ProfileBuilder()
            .WithUserId("user-1")
            .WithPreferredModel("claude-sonnet-4-6")
            .WithSalaryRange(120_000m, 180_000m)
            .WithSearchKeywords("python", "aws", "kubernetes")
            .Build();

        await repo.AddAsync(profile);

        var fresh = await repo.GetByIdAsync(profile.Id, "user-1");
        fresh.Should().NotBeNull();
        fresh!.PreferredModel.Should().Be("claude-sonnet-4-6");
        fresh.DesiredSalaryMin.Should().Be(120_000m);
        fresh.DesiredSalaryMax.Should().Be(180_000m);
        fresh.SearchKeywords.Should().BeEquivalentTo(["python", "aws", "kubernetes"]);
    }

    [Fact]
    public async Task GetAllAsync_IsScopedToUser()
    {
        using var fx = new SqliteFixture();
        await fx.SeedUserAsync("user-a");
        await fx.SeedUserAsync("user-b");
        await using var db = fx.CreateContext();
        var repo = new ProfileRepository(db);

        await repo.AddAsync(new ProfileBuilder().WithUserId("user-a").WithName("A1").Build());
        await repo.AddAsync(new ProfileBuilder().WithUserId("user-a").WithName("A2").Build());
        await repo.AddAsync(new ProfileBuilder().WithUserId("user-b").WithName("B1").Build());

        var aProfiles = await repo.GetAllAsync("user-a");
        aProfiles.Should().HaveCount(2);
        aProfiles.Select(p => p.Name).Should().BeEquivalentTo(["A1", "A2"]);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullWhenProfileOwnedByDifferentUser()
    {
        using var fx = new SqliteFixture();
        await fx.SeedUserAsync("user-a");
        await fx.SeedUserAsync("user-b");
        await using var db = fx.CreateContext();
        var repo = new ProfileRepository(db);

        var bProfile = new ProfileBuilder().WithUserId("user-b").Build();
        await repo.AddAsync(bProfile);

        var leak = await repo.GetByIdAsync(bProfile.Id, "user-a");
        leak.Should().BeNull();
    }

    [Fact]
    public async Task DeletingUser_CascadesToProfilesAndDependents()
    {
        using var fx = new SqliteFixture();
        await fx.SeedUserAsync("doomed-user");
        await using var db = fx.CreateContext();

        var profile = new ProfileBuilder().WithUserId("doomed-user").Build();
        var job = new JobBuilder().Build();
        db.SearchProfiles.Add(profile);
        db.Jobs.Add(job);
        db.AiScores.Add(new AiScoreBuilder().ForJob(job.Id).ForProfile(profile.Id).Build());
        db.UserRatings.Add(new UserRatingBuilder().ForJob(job.Id).ForProfile(profile.Id).Build());
        await db.SaveChangesAsync();

        var user = await db.Users.FindAsync("doomed-user");
        db.Users.Remove(user!);
        await db.SaveChangesAsync();

        (await db.SearchProfiles.AnyAsync(p => p.Id == profile.Id)).Should().BeFalse();
        (await db.AiScores.AnyAsync(s => s.ProfileId == profile.Id)).Should().BeFalse();
        (await db.UserRatings.AnyAsync(r => r.ProfileId == profile.Id)).Should().BeFalse();
        // Jobs are not user-owned and remain
        (await db.Jobs.AnyAsync(j => j.Id == job.Id)).Should().BeTrue();
    }
}
