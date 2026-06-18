using JobScout.Infrastructure.Repositories;
using JobScout.Infrastructure.Tests.Builders;
using JobScout.Infrastructure.Tests.Fixtures;

namespace JobScout.Infrastructure.Tests.Repositories;

public class FilterPresetRepositoryTests
{
    private static async Task<Guid> SeedProfileAsync(SqliteFixture fx, string userId, string name)
    {
        await fx.SeedUserAsync(userId);
        await using var db = fx.CreateContext();
        var profile = new ProfileBuilder().WithUserId(userId).WithName(name).Build();
        db.SearchProfiles.Add(profile);
        await db.SaveChangesAsync();
        return profile.Id;
    }

    private static FilterPreset NewPreset(Guid profileId, string name) => new()
    {
        Id = Guid.NewGuid(),
        ProfileId = profileId,
        Name = name,
        Source = JobSource.RemoteOK,
        MinScore = 8m,
        Query = "azure",
        SortBy = JobSortBy.PostedDate,
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task AddThenGetByProfile_RoundTripsAllFields()
    {
        using var fx = new SqliteFixture();
        var profileId = await SeedProfileAsync(fx, "user-1", "P1");

        await using (var db = fx.CreateContext())
            await new FilterPresetRepository(db).AddAsync(NewPreset(profileId, "Remote 8+"));

        await using var read = fx.CreateContext();
        var presets = await new FilterPresetRepository(read).GetByProfileAsync(profileId);

        presets.Should().ContainSingle();
        var p = presets[0];
        p.Name.Should().Be("Remote 8+");
        p.Source.Should().Be(JobSource.RemoteOK);
        p.MinScore.Should().Be(8m);
        p.Query.Should().Be("azure");
        p.SortBy.Should().Be(JobSortBy.PostedDate);
    }

    [Fact]
    public async Task GetByProfile_IsScopedToProfile()
    {
        using var fx = new SqliteFixture();
        var p1 = await SeedProfileAsync(fx, "user-1", "P1");
        var p2 = await SeedProfileAsync(fx, "user-1", "P2");

        await using (var db = fx.CreateContext())
        {
            var repo = new FilterPresetRepository(db);
            await repo.AddAsync(NewPreset(p1, "A"));
            await repo.AddAsync(NewPreset(p1, "B"));
            await repo.AddAsync(NewPreset(p2, "C"));
        }

        await using var read = fx.CreateContext();
        var forP1 = await new FilterPresetRepository(read).GetByProfileAsync(p1);
        forP1.Select(p => p.Name).Should().BeEquivalentTo(["A", "B"]);
    }

    [Fact]
    public async Task UpdateThenDelete_Work()
    {
        using var fx = new SqliteFixture();
        var profileId = await SeedProfileAsync(fx, "user-1", "P1");
        var preset = NewPreset(profileId, "Original");

        await using (var db = fx.CreateContext())
            await new FilterPresetRepository(db).AddAsync(preset);

        await using (var db = fx.CreateContext())
        {
            var repo = new FilterPresetRepository(db);
            var loaded = await repo.GetByIdAsync(preset.Id);
            loaded!.Name = "Renamed";
            await repo.UpdateAsync(loaded);
        }

        await using (var db = fx.CreateContext())
            (await new FilterPresetRepository(db).GetByIdAsync(preset.Id))!.Name.Should().Be("Renamed");

        await using (var db = fx.CreateContext())
            await new FilterPresetRepository(db).DeleteAsync(preset.Id);

        await using var final = fx.CreateContext();
        (await new FilterPresetRepository(final).GetByIdAsync(preset.Id)).Should().BeNull();
    }

    [Fact]
    public async Task DeletingProfile_CascadesToPresets()
    {
        using var fx = new SqliteFixture();
        var profileId = await SeedProfileAsync(fx, "user-1", "P1");

        await using (var db = fx.CreateContext())
            await new FilterPresetRepository(db).AddAsync(NewPreset(profileId, "X"));

        await using (var db = fx.CreateContext())
        {
            var profile = await db.SearchProfiles.FindAsync(profileId);
            db.SearchProfiles.Remove(profile!);
            await db.SaveChangesAsync();
        }

        await using var read = fx.CreateContext();
        (await read.FilterPresets.AnyAsync(p => p.ProfileId == profileId)).Should().BeFalse();
    }
}
