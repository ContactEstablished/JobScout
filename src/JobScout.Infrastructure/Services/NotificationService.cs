using JobScout.Core.Enums;
using JobScout.Core.Interfaces;
using JobScout.Core.Models;
using JobScout.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobScout.Infrastructure.Services;

public class NotificationService(
    JobScoutDbContext db,
    ILogger<NotificationService> logger) : INotificationService
{
    private const decimal StrongFitThreshold = 8m;
    private const decimal InstantAlertThreshold = 9m;

    public async Task<Notification?> CreateAsync(
        string userId,
        NotificationType type,
        string title,
        string message,
        Guid? profileId = null,
        Guid? jobId = null,
        Guid? applicationId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(userId)) return null;

        var prefs = await GetOrCreatePreferencesAsync(userId, ct);
        if (!IsInAppEnabled(prefs, type))
            return null;

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProfileId = profileId,
            Type = type,
            Title = Truncate(title, 200),
            Message = Truncate(message, 1000),
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
            RelatedJobId = jobId,
            RelatedApplicationId = applicationId
        };

        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct);
        return notification;
    }

    public async Task<IReadOnlyList<Notification>> GetForUserAsync(
        string userId, bool unreadOnly = false, int take = 50, CancellationToken ct = default)
    {
        var query = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (unreadOnly) query = query.Where(n => !n.IsRead);

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<int> GetUnreadCountAsync(string userId, CancellationToken ct = default)
        => db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);

    public async Task<bool> MarkReadAsync(Guid notificationId, string userId, CancellationToken ct = default)
    {
        var n = await db.Notifications.FirstOrDefaultAsync(
            x => x.Id == notificationId && x.UserId == userId, ct);
        if (n is null) return false;
        if (n.IsRead) return true;

        n.IsRead = true;
        n.ReadAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> MarkAllReadAsync(string userId, CancellationToken ct = default)
    {
        var unread = await db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = now;
        }
        await db.SaveChangesAsync(ct);
        return unread.Count;
    }

    public async Task<bool> DeleteAsync(Guid notificationId, string userId, CancellationToken ct = default)
    {
        var n = await db.Notifications.FirstOrDefaultAsync(
            x => x.Id == notificationId && x.UserId == userId, ct);
        if (n is null) return false;
        db.Notifications.Remove(n);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task OnIngestionCompleteAsync(
        SearchProfile profile, IngestionResult result, CancellationToken ct = default)
    {
        if (result.NewJobsFound <= 0) return;

        var bySource = result.BySource.Count > 0
            ? " — " + string.Join(", ", result.BySource.Select(kv => $"{kv.Value} from {kv.Key}"))
            : "";

        await CreateAsync(
            profile.UserId,
            NotificationType.IngestionComplete,
            $"{result.NewJobsFound} new job{(result.NewJobsFound == 1 ? "" : "s")} ingested",
            $"Profile \"{profile.Name}\" picked up {result.NewJobsFound} new posting{(result.NewJobsFound == 1 ? "" : "s")}{bySource}.",
            profileId: profile.Id,
            ct: ct);
    }

    public async Task OnHighScoreCreatedAsync(
        AiScore score, Job job, SearchProfile profile, CancellationToken ct = default)
    {
        if (score.Score < StrongFitThreshold) return;

        await CreateAsync(
            profile.UserId,
            NotificationType.NewStrongFit,
            $"Strong match: {job.Title} at {job.Company}",
            $"Score {score.Score:F1}/10 for profile \"{profile.Name}\".",
            profileId: profile.Id,
            jobId: job.Id,
            ct: ct);
    }

    public async Task OnApplicationStatusChangedAsync(
        JobApplication application,
        ApplicationStatus oldStatus,
        ApplicationStatus newStatus,
        string userId,
        CancellationToken ct = default)
    {
        var jobTitle = application.Job?.Title ?? "your application";
        await CreateAsync(
            userId,
            NotificationType.ApplicationStatusChange,
            $"Application moved: {oldStatus} → {newStatus}",
            $"\"{jobTitle}\" status updated to {newStatus}.",
            profileId: application.ProfileId,
            jobId: application.JobId,
            applicationId: application.Id,
            ct: ct);
    }

    private async Task<NotificationPreferences> GetOrCreatePreferencesAsync(string userId, CancellationToken ct)
    {
        var prefs = await db.NotificationPreferences.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (prefs is null)
        {
            prefs = new NotificationPreferences { UserId = userId };
            db.NotificationPreferences.Add(prefs);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                logger.LogDebug(ex, "Notification prefs race for {UserId}; reloading", userId);
                db.Entry(prefs).State = EntityState.Detached;
                prefs = await db.NotificationPreferences.FirstAsync(p => p.UserId == userId, ct);
            }
        }
        return prefs;
    }

    private static bool IsInAppEnabled(NotificationPreferences prefs, NotificationType type) => type switch
    {
        NotificationType.NewStrongFit => prefs.InAppNewStrongFit,
        NotificationType.ScoreUpdate => prefs.InAppScoreUpdate,
        NotificationType.IngestionComplete => prefs.InAppIngestionComplete,
        NotificationType.ApplicationStatusChange => prefs.InAppApplicationStatusChange,
        _ => true
    };

    private static string Truncate(string? text, int max)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= max ? text : text[..max];
    }

    public static bool IsWithinQuietHours(NotificationPreferences prefs, DateTimeOffset utcNow)
    {
        if (prefs.QuietHoursStart is not TimeOnly start || prefs.QuietHoursEnd is not TimeOnly end)
            return false;
        if (start == end) return false;

        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(prefs.TimeZoneId); }
        catch { tz = TimeZoneInfo.Utc; }

        var local = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, tz).DateTime);

        return start < end
            ? local >= start && local < end
            : local >= start || local < end;
    }
}
