using JobScout.Infrastructure.Services;
using JobScout.Infrastructure.Tests.Builders;
using JobScout.Infrastructure.Tests.Fixtures;

namespace JobScout.Infrastructure.Tests.Services;

public class NotificationServiceTests
{
    [Fact]
    public async Task OnIngestionCompleteAsync_WhenNoNewJobs_DoesNotPersistNotification()
    {
        using var fx = new SqliteFixture();
        await fx.SeedUserAsync();
        await using var db = fx.CreateContext();

        var profile = new ProfileBuilder().Build();
        db.SearchProfiles.Add(profile);
        await db.SaveChangesAsync();

        var service = new NotificationService(db, NullLogger<NotificationService>.Instance);
        await service.OnIngestionCompleteAsync(profile, new IngestionResult { NewJobsFound = 0 });

        var rows = await db.Notifications.CountAsync();
        rows.Should().Be(0);
    }

    [Fact]
    public async Task OnIngestionCompleteAsync_WhenNewJobsExist_CreatesNotificationRow()
    {
        using var fx = new SqliteFixture();
        await fx.SeedUserAsync();
        await using var db = fx.CreateContext();

        var profile = new ProfileBuilder().Build();
        db.SearchProfiles.Add(profile);
        await db.SaveChangesAsync();

        var service = new NotificationService(db, NullLogger<NotificationService>.Instance);
        await service.OnIngestionCompleteAsync(profile, new IngestionResult
        {
            NewJobsFound = 3,
            BySource = new() { ["RemoteOK"] = 3 }
        });

        var notification = await db.Notifications.SingleAsync();
        notification.Type.Should().Be(NotificationType.IngestionComplete);
        notification.UserId.Should().Be(profile.UserId);
        notification.ProfileId.Should().Be(profile.Id);
        notification.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_RespectsInAppPreferenceToggle()
    {
        using var fx = new SqliteFixture();
        await fx.SeedUserAsync();
        await using var db = fx.CreateContext();

        db.NotificationPreferences.Add(new NotificationPreferences
        {
            UserId = "test-user",
            InAppNewStrongFit = false
        });
        await db.SaveChangesAsync();

        var service = new NotificationService(db, NullLogger<NotificationService>.Instance);
        var result = await service.CreateAsync("test-user", NotificationType.NewStrongFit, "T", "M");

        result.Should().BeNull();
        (await db.Notifications.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task MarkReadAsync_OnlyAffectsOwnedNotifications()
    {
        using var fx = new SqliteFixture();
        await fx.SeedUserAsync("user-a");
        await fx.SeedUserAsync("user-b");
        await using var db = fx.CreateContext();

        var n = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = "user-a",
            Type = NotificationType.NewStrongFit,
            Title = "T",
            Message = "M",
            CreatedAt = DateTime.UtcNow
        };
        db.Notifications.Add(n);
        await db.SaveChangesAsync();

        var service = new NotificationService(db, NullLogger<NotificationService>.Instance);

        var attemptedByOther = await service.MarkReadAsync(n.Id, "user-b");
        attemptedByOther.Should().BeFalse();

        var attemptedByOwner = await service.MarkReadAsync(n.Id, "user-a");
        attemptedByOwner.Should().BeTrue();

        var reloaded = await db.Notifications.FindAsync(n.Id);
        reloaded!.IsRead.Should().BeTrue();
        reloaded.ReadAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData(22, 0, 6, 0, 0, 0, true)]    // wrap-around: 22:00→06:00, now 00:00 → inside
    [InlineData(22, 0, 6, 0, 10, 0, false)]  // wrap-around: now 10:00 → outside
    [InlineData(8, 0, 10, 0, 9, 0, true)]    // normal: now 09:00 inside [08:00, 10:00)
    [InlineData(8, 0, 10, 0, 11, 0, false)]  // normal: now 11:00 outside
    public void IsWithinQuietHours_BehavesCorrectly(int sH, int sM, int eH, int eM, int nH, int nM, bool expected)
    {
        var prefs = new NotificationPreferences
        {
            QuietHoursStart = new TimeOnly(sH, sM),
            QuietHoursEnd = new TimeOnly(eH, eM),
            TimeZoneId = "UTC"
        };
        var now = new DateTimeOffset(2026, 5, 23, nH, nM, 0, TimeSpan.Zero);
        NotificationService.IsWithinQuietHours(prefs, now).Should().Be(expected);
    }
}
