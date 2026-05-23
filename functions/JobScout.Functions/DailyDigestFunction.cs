using JobScout.Core.Interfaces;
using JobScout.Core.Models;
using JobScout.Infrastructure.Data;
using JobScout.Infrastructure.Email.Templates;
using JobScout.Infrastructure.Identity;
using JobScout.Infrastructure.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JobScout.Functions;

public class DailyDigestFunction(
    JobScoutDbContext db,
    IEmailSender email,
    IConfiguration config,
    ILogger<DailyDigestFunction> logger)
{
    // Runs at 13:00 UTC daily (~9am US Eastern, ~6am US Pacific)
    [Function("DailyDigestTimer")]
    public async Task Run([TimerTrigger("0 0 13 * * *")] TimerInfo timer)
    {
        var appBaseUrl = config["AppBaseUrl"] ?? "https://localhost:7036";
        var now = DateTimeOffset.UtcNow;
        var since = now.AddHours(-24).UtcDateTime;

        var optedIn = await db.NotificationPreferences
            .Where(p => p.EmailDailyDigest)
            .ToListAsync();

        if (optedIn.Count == 0)
        {
            logger.LogInformation("Daily digest: no users opted in");
            return;
        }

        foreach (var prefs in optedIn)
        {
            try
            {
                if (NotificationService.IsWithinQuietHours(prefs, now))
                {
                    logger.LogInformation("Daily digest: skipping {UserId} (quiet hours)", prefs.UserId);
                    continue;
                }

                var user = await db.Set<ApplicationUser>().FirstOrDefaultAsync(u => u.Id == prefs.UserId);
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
                ).Take(10).ToListAsync();

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
                });

                logger.LogInformation("Daily digest sent to {Email} ({Count} jobs)", user.Email, digestJobs.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Daily digest failed for {UserId}", prefs.UserId);
            }
        }
    }
}
