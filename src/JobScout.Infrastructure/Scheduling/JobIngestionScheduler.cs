using JobScout.Core.Interfaces;
using JobScout.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace JobScout.Infrastructure.Scheduling;

/// <summary>
/// Runs job ingestion + AI scoring every 4 hours.
/// Replaces the Azure Function `JobIngestionTimer`.
/// </summary>
public class JobIngestionScheduler(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<JobIngestionScheduler> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(4);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Scheduling:Enabled", true))
        {
            logger.LogInformation("JobIngestionScheduler disabled via config");
            return;
        }

        logger.LogInformation("JobIngestionScheduler started — interval {Interval}", Interval);

        // Delay a bit so the API has time to come up.
        try { await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken); }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not TaskCanceledException)
            {
                logger.LogError(ex, "JobIngestionScheduler tick failed");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (TaskCanceledException) { return; }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JobScoutDbContext>();
        var ingestion = scope.ServiceProvider.GetRequiredService<IJobIngestionService>();
        var scoring = scope.ServiceProvider.GetRequiredService<IAiScoringService>();

        var profiles = await db.SearchProfiles
            .Where(p => p.IsActive)
            .ToListAsync(ct);

        if (profiles.Count == 0)
        {
            logger.LogInformation("Ingestion tick: no active profiles");
            return;
        }

        foreach (var profile in profiles)
        {
            if (ct.IsCancellationRequested) return;

            try
            {
                logger.LogInformation("Ingestion tick: profile {ProfileId} ({Name})", profile.Id, profile.Name);
                var result = await ingestion.IngestAsync(profile);
                logger.LogInformation(
                    "Profile {ProfileId}: {New} new, {Dupes} exact dupes",
                    profile.Id, result.NewJobsFound, result.Duplicates);

                if (result.NewJobsFound > 0)
                {
                    var newJobs = await db.Jobs
                        .Where(j => j.IsActive && !j.AiScores.Any(s => s.ProfileId == profile.Id))
                        .ToListAsync(ct);

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
