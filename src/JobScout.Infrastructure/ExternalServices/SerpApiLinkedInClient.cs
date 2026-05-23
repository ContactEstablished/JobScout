using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JobScout.Core.Enums;
using JobScout.Core.Interfaces;
using JobScout.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JobScout.Infrastructure.ExternalServices;

public class SerpApiLinkedInClient(
    HttpClient http,
    IConfiguration config,
    ILogger<SerpApiLinkedInClient> logger) : IJobBoardClient
{
    public JobSource Source => JobSource.LinkedIn;
    public async Task<IReadOnlyList<Job>> FetchJobsAsync(SearchProfile profile, CancellationToken ct = default)
    {
        var apiKey = config["SerpApi:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            logger.LogInformation("SerpAPI key not configured — skipping LinkedIn");
            return [];
        }

        var query  = BuildQuery(profile);
        var jobs   = new List<Job>();
        const int maxPages = 3;

        for (int page = 0; page < maxPages; page++)
        {
            try
            {
                var location = !string.IsNullOrWhiteSpace(profile.LocationPreference)
                    ? profile.LocationPreference
                    : "United States";

                var url = "https://serpapi.com/search.json" +
                          $"?engine=linkedin_jobs" +
                          $"&q={Uri.EscapeDataString(query)}" +
                          $"&location={Uri.EscapeDataString(location)}" +
                          $"&start={page * 10}" +
                          $"&api_key={apiKey}";

                var response = await http.GetFromJsonAsync<SerpApiResponse>(url, ct);
                var listings = response?.JobsResults;
                if (listings is null || listings.Length == 0) break;

                foreach (var r in listings)
                {
                    var locType = r.Location?.Contains("Remote", StringComparison.OrdinalIgnoreCase) == true
                        ? LocationType.Remote : LocationType.OnSite;

                    jobs.Add(new Job
                    {
                        Id           = Guid.NewGuid(),
                        ExternalId   = r.JobId ?? Guid.NewGuid().ToString(),
                        Title        = r.Title ?? "Unknown",
                        Company      = r.CompanyName ?? "Unknown",
                        Location     = r.Location,
                        LocationType = locType,
                        JobType      = JobType.FullTime,
                        Description  = r.Description ?? "",
                        Tags         = "[]",
                        PostedAt     = null,
                        DiscoveredAt = DateTime.UtcNow,
                        Source       = JobSource.LinkedIn,
                        SourceUrl    = r.Link ?? "",
                        IsActive     = true
                    });
                }

                if (listings.Length < 10) break;
                await Task.Delay(1000, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SerpAPI page {Page} failed", page);
                break;
            }
        }

        logger.LogInformation("LinkedIn (SerpAPI): fetched {Count} jobs", jobs.Count);
        return jobs;
    }

    private static string BuildQuery(SearchProfile profile)
    {
        if (profile.SearchKeywords.Count > 0)
            return string.Join(" ", profile.SearchKeywords)[..Math.Min(string.Join(" ", profile.SearchKeywords).Length, 100)];

        var text = $"{profile.Name} {profile.Description}".Trim();
        return string.IsNullOrWhiteSpace(text) ? "software engineer" : text[..Math.Min(text.Length, 100)];
    }

    private record SerpApiResponse(
        [property: JsonPropertyName("jobs_results")] SerpApiJob[]? JobsResults);

    private record SerpApiJob(
        [property: JsonPropertyName("job_id")]      string? JobId,
        [property: JsonPropertyName("title")]       string? Title,
        [property: JsonPropertyName("company_name")] string? CompanyName,
        [property: JsonPropertyName("location")]    string? Location,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("link")]        string? Link);
}
