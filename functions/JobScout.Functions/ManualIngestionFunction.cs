using JobScout.Core.Interfaces;
using JobScout.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobScout.Functions;

public class ManualIngestionFunction(
    JobScoutDbContext db,
    IJobIngestionService ingestion,
    IAiScoringService scoring,
    ILogger<ManualIngestionFunction> logger)
{
    [Function("ManualIngest")]
    public async Task<IActionResult> RunHttp(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "ingest")] HttpRequest req)
    {
        if (!req.Query.TryGetValue("profileId", out var raw) || !Guid.TryParse(raw, out var profileId))
            return new BadRequestObjectResult("Query parameter 'profileId' must be a valid GUID.");

        var profile = await db.SearchProfiles.FindAsync(profileId);
        if (profile is null)
            return new NotFoundObjectResult($"Profile {profileId} not found.");

        logger.LogInformation("Manual ingestion triggered for profile {ProfileId}", profileId);

        var result = await ingestion.IngestAsync(profile);

        if (result.NewJobsFound > 0)
        {
            var newJobs = await db.Jobs
                .Where(j => j.IsActive && !j.AiScores.Any(s => s.ProfileId == profileId))
                .ToListAsync();

            await scoring.BatchScoreAsync(newJobs, profile);
        }

        return new OkObjectResult(new
        {
            profileId,
            result.NewJobsFound,
            result.Duplicates,
            result.BySource
        });
    }
}
