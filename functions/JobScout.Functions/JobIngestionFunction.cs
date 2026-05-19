using JobScout.Core.Interfaces;
using JobScout.Infrastructure.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobScout.Functions;

public class JobIngestionFunction(
    JobScoutDbContext db,
    IJobIngestionService ingestion,
    IAiScoringService scoring,
    ILogger<JobIngestionFunction> logger)
{
    // Runs every 4 hours
    [Function("JobIngestionTimer")]
    public async Task RunTimer([TimerTrigger("0 0 */4 * * *")] TimerInfo timer)
    {
        logger.LogInformation("Job ingestion timer fired at {Time}", DateTime.UtcNow);

        var profiles = await db.SearchProfiles
            .Where(p => p.IsActive)
            .ToListAsync();

        if (profiles.Count == 0)
        {
            logger.LogInformation("No active profiles found — skipping ingestion");
            return;
        }

        foreach (var profile in profiles)
        {
            try
            {
                logger.LogInformation("Ingesting for profile {ProfileId} ({Name})", profile.Id, profile.Name);
                var result = await ingestion.IngestAsync(profile);
                logger.LogInformation(
                    "Profile {ProfileId}: {New} new jobs, {Dupes} duplicates",
                    profile.Id, result.NewJobsFound, result.Duplicates);

                if (result.NewJobsFound > 0)
                {
                    var newJobs = await db.Jobs
                        .Where(j => j.IsActive && !j.AiScores.Any(s => s.ProfileId == profile.Id))
                        .ToListAsync();

                    await scoring.BatchScoreAsync(newJobs, profile);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ingestion failed for profile {ProfileId}", profile.Id);
            }
        }
    }
}
