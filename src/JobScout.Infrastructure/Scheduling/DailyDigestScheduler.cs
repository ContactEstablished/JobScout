using JobScout.Core.Interfaces;
using JobScout.Core.Models;
using JobScout.Infrastructure.Data;
using JobScout.Infrastructure.Email.Templates;
using JobScout.Infrastructure.Identity;
using JobScout.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JobScout.Infrastructure.Scheduling;

/// <summary>
/// Sends the daily digest email at 13:00 UTC each day.
/// Replaces the Azure Function `DailyDigestTimer`.
/// </summary>
public class DailyDigestScheduler(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<DailyDigestScheduler> logger) : BackgroundService
{
    private static readonly TimeOnly TargetUtc = new(13, 0);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Scheduling:Enabled", true))
        {
            logger.LogInformation("DailyDigestScheduler disabled via config");
            return;
        }

        logger.LogInformation("DailyDigestScheduler started — fires daily at {Time} UTC", TargetUtc);

        while (!stoppingToken.IsCancellationRequested)
        {
            var wait = TimeUntilNextRun(DateTimeOffset.UtcNow);
            logger.LogInformation("DailyDigestScheduler sleeping {Wait} until next run", wait);
            try { await Task.Delay(wait, stoppingToken); }
            catch (TaskCanceledException) { return; }

            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not TaskCanceledException)
            {
                logger.LogError(ex, "DailyDigestScheduler tick failed");
            }
        }
    }

    private static TimeSpan TimeUntilNextRun(DateTimeOffset now)
    {
        var nextRun = new DateTimeOffset(
            now.Year, now.Month, now.Day,
            TargetUtc.Hour, TargetUtc.Minute, 0, TimeSpan.Zero);
        if (nextRun <= now) nextRun = nextRun.AddDays(1);
        return nextRun - now;
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JobScoutDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var appBaseUrl = config["AppBaseUrl"] ?? "http://localhost:5000";

        var now = DateTimeOffset.UtcNow;
        var since = now.AddHours(-24).UtcDateTime;

        var optedIn = await db.NotificationPreferences
            .Where(p => p.EmailDailyDigest)
            .ToListAsync(ct);

        if (optedIn.Count == 0)
        {
            logger.LogInformation("Daily digest: no users opted in");
            return;
        }

        foreach (var prefs in optedIn)
        {
            if (ct.IsCancellationRequested) return;

            try
            {
                if (NotificationService.IsWithinQuietHours(prefs, now))
                {
                    logger.LogInformation("Daily digest: skipping {UserId} (quiet hours)", prefs.UserId);
                    continue;
                }

                var user = await db.Set<ApplicationUser>().FirstOrDefaultAsync(u => u.Id == prefs.UserId, ct);
                if (user is null || string.IsNullOrEmpty(user.Email))
                {
                    logger.LogWarning("Daily digest: user {UserId} missing or has no email", prefs.UserId);
                    continue;
                }

                var rawJobs = await (
                    from s in db.AiScores.AsNoTracking()
                    join j in db.Jobs.AsNoTracking() on s.JobId equals j.Id
                    join p in db.SearchProfiles.AsNoTracking() on s.ProfileId equals p.Id
                    where p.UserId == prefs.UserId
                       && s.Score >= 8m
                       && s.ScoredAt >= since
                    orderby s.Score descending
                    select new { Job = j, s.Score }
                ).Take(10).ToListAsync(ct);

                if (rawJobs.Count == 0)
                {
                    logger.LogInformation("Daily digest: no strong fits for {UserId}", prefs.UserId);
                    continue;
                }

                var digestJobs = rawJobs.Select(x => new DigestJob(x.Job, x.Score)).ToList();
                var displayName = string.IsNullOrEmpty(user.DisplayName) ? "there" : user.DisplayName;
                var tpl = DailyDigestTemplate.Render(displayName, digestJobs, appBaseUrl);

                await email.SendAsync(new EmailMessage
                {
                    ToAddress = user.Email,
                    ToName = user.DisplayName,
                    Subject = tpl.Subject,
                    HtmlBody = tpl.HtmlBody,
                    PlainTextBody = tpl.PlainTextBody
                }, ct);

                logger.LogInformation("Daily digest sent to {Email} ({Count} jobs)", user.Email, digestJobs.Count);
            }
            catch (Exception ex) when (ex is not TaskCanceledException)
            {
                logger.LogError(ex, "Daily digest failed for {UserId}", prefs.UserId);
            }
        }
    }
}
