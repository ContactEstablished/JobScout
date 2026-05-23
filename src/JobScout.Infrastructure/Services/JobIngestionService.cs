using JobScout.Core.Interfaces;
using JobScout.Core.Models;
using Microsoft.Extensions.Logging;

namespace JobScout.Infrastructure.Services;

public class JobIngestionService(
    IEnumerable<IJobBoardClient> clients,
    IJobRepository jobs,
    IDeduplicationService deduplication,
    INotificationService notifications,
    ILogger<JobIngestionService> logger) : IJobIngestionService
{
    public async Task<IngestionResult> IngestAsync(SearchProfile profile)
    {
        var result = new IngestionResult();

        // Filter clients by preferred sources (empty = use all)
        var activeClients = profile.PreferredSources.Count > 0
            ? clients.Where(c => profile.PreferredSources.Contains(c.Source))
            : clients;

        // Fetch from active clients in parallel
        var fetchTasks = activeClients
            .Select(async c =>
            {
                try { return await c.FetchJobsAsync(profile); }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Client {Client} failed", c.GetType().Name);
                    return (IReadOnlyList<Job>)[];
                }
            });

        var allFetched = (await Task.WhenAll(fetchTasks)).SelectMany(x => x).ToList();
        logger.LogInformation("Ingestion: {Total} total jobs fetched across all sources", allFetched.Count);

        // Deduplicate and save
        foreach (var job in allFetched)
        {
            // 1. Exact dedup: same ExternalId + Source
            var existing = await jobs.GetByExternalIdAsync(job.ExternalId, job.Source);
            if (existing is not null)
            {
                result.Duplicates++;
                continue;
            }

            // 2. Fuzzy dedup: same normalized title + company across different sources
            var fuzzyMatch = await deduplication.FindFuzzyDuplicateAsync(job);
            if (fuzzyMatch is not null)
            {
                job.IsPotentialDuplicate = true;
                job.DuplicateOfJobId     = fuzzyMatch.Id;
                result.FuzzyDuplicates++;
                logger.LogDebug(
                    "Fuzzy duplicate: '{Title}' @ '{Company}' ({Source}) matches job {MatchId}",
                    job.Title, job.Company, job.Source, fuzzyMatch.Id);
            }

            await jobs.AddAsync(job);

            result.NewJobsFound++;
            result.BySource.TryGetValue(job.Source.ToString(), out var count);
            result.BySource[job.Source.ToString()] = count + 1;
        }

        logger.LogInformation(
            "Ingestion complete: {New} new, {Dupes} exact duplicates, {Fuzzy} fuzzy duplicates",
            result.NewJobsFound, result.Duplicates, result.FuzzyDuplicates);

        try
        {
            await notifications.OnIngestionCompleteAsync(profile, result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to emit ingestion notification for profile {ProfileId}", profile.Id);
        }

        return result;
    }
}
