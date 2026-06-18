using JobScout.Core.Enums;
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
/// Sends the weekly summary email Mondays at 14:00 UTC.
/// Replaces the Azure Function `WeeklySummaryTimer`.
/// </summary>
public class WeeklySummaryScheduler(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<WeeklySummaryScheduler> logger) : BackgroundService
{
    private static readonly TimeOnly TargetUtc = new(14, 0);
    private const DayOfWeek TargetDay = DayOfWeek.Monday;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Scheduling:Enabled", true))
        {
            logger.LogInformation("WeeklySummaryScheduler disabled via config");
            return;
        }

        logger.LogInformation(
            "WeeklySummaryScheduler started — fires {Day} {Time} UTC", TargetDay, TargetUtc);

        while (!stoppingToken.IsCancellationRequested)
        {
            var wait = TimeUntilNextRun(DateTimeOffset.UtcNow);
            logger.LogInformation("WeeklySummaryScheduler sleeping {Wait} until next run", wait);
            try { await Task.Delay(wait, stoppingToken); }
            catch (TaskCanceledException) { return; }

            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not TaskCanceledException)
            {
                logger.LogError(ex, "WeeklySummaryScheduler tick failed");
            }
        }
    }

    private static TimeSpan TimeUntilNextRun(DateTimeOffset now)
    {
        var todayTarget = new DateTimeOffset(
            now.Year, now.Month, now.Day,
            TargetUtc.Hour, TargetUtc.Minute, 0, TimeSpan.Zero);

        var daysUntilTarget = ((int)TargetDay - (int)now.DayOfWeek + 7) % 7;
        var nextRun = todayTarget.AddDays(daysUntilTarget);

        if (nextRun <= now) nextRun = nextRun.AddDays(7);
        return nextRun - now;
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JobScoutDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var appBaseUrl = config["AppBaseUrl"] ?? "http://localhost:5000";

        var now = DateTimeOffset.UtcNow;
        var since = now.AddDays(-7).UtcDateTime;

        var optedIn = await db.NotificationPreferences
            .Where(p => p.EmailWeeklySummary)
            .ToListAsync(ct);

        if (optedIn.Count == 0)
        {
            logger.LogInformation("Weekly summary: no users opted in");
            return;
        }

        foreach (var prefs in optedIn)
        {
            if (ct.IsCancellationRequested) return;

            try
            {
                if (NotificationService.IsWithinQuietHours(prefs, now))
                {
                    logger.LogInformation("Weekly summary: skipping {UserId} (quiet hours)", prefs.UserId);
                    continue;
                }

                var user = await db.Set<ApplicationUser>().FirstOrDefaultAsync(u => u.Id == prefs.UserId, ct);
                if (user is null || string.IsNullOrEmpty(user.Email)) continue;

                var profileIds = await db.SearchProfiles
                    .Where(p => p.UserId == prefs.UserId)
                    .Select(p => p.Id)
                    .ToListAsync(ct);

                if (profileIds.Count == 0) continue;

                var totalJobs = await db.Jobs
                    .CountAsync(j => j.DiscoveredAt >= since
                                  && j.AiScores.Any(s => profileIds.Contains(s.ProfileId)), ct);

                var strongFits = await db.AiScores
                    .CountAsync(s => profileIds.Contains(s.ProfileId)
                                  && s.Score >= 8m
                                  && s.ScoredAt >= since, ct);

                var apps = await db.JobApplications
                    .Where(a => profileIds.Contains(a.ProfileId) && a.AppliedAt >= since)
                    .GroupBy(a => a.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync(ct);

                int CountFor(ApplicationStatus s) => apps.FirstOrDefault(x => x.Status == s)?.Count ?? 0;

                var rawTop = await (
                    from s in db.AiScores.AsNoTracking()
                    join j in db.Jobs.AsNoTracking() on s.JobId equals j.Id
                    where profileIds.Contains(s.ProfileId) && s.ScoredAt >= since
                    orderby s.Score descending
                    select new { Job = j, s.Score }
                ).Take(5).ToListAsync(ct);

                var topJobs = rawTop.Select(x => new DigestJob(x.Job, x.Score)).ToList();
                var displayName = string.IsNullOrEmpty(user.DisplayName) ? "there" : user.DisplayName;

                var data = new WeeklySummaryData(
                    DisplayName: displayName,
                    TotalJobs: totalJobs,
                    StrongFits: strongFits,
                    Applied: CountFor(ApplicationStatus.Applied),
                    Interviewing: CountFor(ApplicationStatus.Interviewing),
                    Offered: CountFor(ApplicationStatus.Offered),
                    TopJobs: topJobs);

                var tpl = WeeklySummaryTemplate.Render(data, appBaseUrl);

                await email.SendAsync(new EmailMessage
                {
                    ToAddress = user.Email,
                    ToName = user.DisplayName,
                    Subject = tpl.Subject,
                    HtmlBody = tpl.HtmlBody,
                    PlainTextBody = tpl.PlainTextBody
                }, ct);

                logger.LogInformation("Weekly summary sent to {Email}", user.Email);
            }
            catch (Exception ex) when (ex is not TaskCanceledException)
            {
                logger.LogError(ex, "Weekly summary failed for {UserId}", prefs.UserId);
            }
        }
    }
}
