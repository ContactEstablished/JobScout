using JobScout.Core.Enums;
using JobScout.Core.Interfaces;
using JobScout.Infrastructure.Data;
using JobScout.Infrastructure.Email.Templates;
using JobScout.Infrastructure.Identity;
using JobScout.Infrastructure.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JobScout.Functions;

public class WeeklySummaryFunction(
    JobScoutDbContext db,
    IEmailSender email,
    IConfiguration config,
    ILogger<WeeklySummaryFunction> logger)
{
    // Monday 14:00 UTC
    [Function("WeeklySummaryTimer")]
    public async Task Run([TimerTrigger("0 0 14 * * MON")] TimerInfo timer)
    {
        var appBaseUrl = config["AppBaseUrl"] ?? "https://localhost:7036";
        var now = DateTimeOffset.UtcNow;
        var since = now.AddDays(-7).UtcDateTime;

        var optedIn = await db.NotificationPreferences
            .Where(p => p.EmailWeeklySummary)
            .ToListAsync();

        if (optedIn.Count == 0)
        {
            logger.LogInformation("Weekly summary: no users opted in");
            return;
        }

        foreach (var prefs in optedIn)
        {
            try
            {
                if (NotificationService.IsWithinQuietHours(prefs, now))
                {
                    logger.LogInformation("Weekly summary: skipping {UserId} (quiet hours)", prefs.UserId);
                    continue;
                }

                var user = await db.Set<ApplicationUser>().FirstOrDefaultAsync(u => u.Id == prefs.UserId);
                if (user is null || string.IsNullOrEmpty(user.Email))
                    continue;

                var profileIds = await db.SearchProfiles
                    .Where(p => p.UserId == prefs.UserId)
                    .Select(p => p.Id)
                    .ToListAsync();

                if (profileIds.Count == 0)
                    continue;

                var totalJobs = await db.Jobs
                    .CountAsync(j => j.DiscoveredAt >= since
                                  && j.AiScores.Any(s => profileIds.Contains(s.ProfileId)));

                var strongFits = await db.AiScores
                    .CountAsync(s => profileIds.Contains(s.ProfileId)
                                  && s.Score >= 8m
                                  && s.ScoredAt >= since);

                var apps = await db.JobApplications
                    .Where(a => profileIds.Contains(a.ProfileId) && a.AppliedAt >= since)
                    .GroupBy(a => a.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync();

                int CountFor(ApplicationStatus s) => apps.FirstOrDefault(x => x.Status == s)?.Count ?? 0;

                var rawTop = await (
                    from s in db.AiScores.AsNoTracking()
                    join j in db.Jobs.AsNoTracking() on s.JobId equals j.Id
                    where profileIds.Contains(s.ProfileId) && s.ScoredAt >= since
                    orderby s.Score descending
                    select new { Job = j, s.Score }
                ).Take(5).ToListAsync();

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
                });

                logger.LogInformation("Weekly summary sent to {Email}", user.Email);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Weekly summary failed for {UserId}", prefs.UserId);
            }
        }
    }
}
