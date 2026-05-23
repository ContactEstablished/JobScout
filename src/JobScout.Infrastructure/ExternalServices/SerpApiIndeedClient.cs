using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JobScout.Core.Enums;
using JobScout.Core.Interfaces;
using JobScout.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JobScout.Infrastructure.ExternalServices;

public class SerpApiIndeedClient(
    HttpClient http,
    IConfiguration config,
    ILogger<SerpApiIndeedClient> logger) : IJobBoardClient
{
    public JobSource Source => JobSource.Indeed;

    public async Task<IReadOnlyList<Job>> FetchJobsAsync(SearchProfile profile, CancellationToken ct = default)
    {
        var apiKey = config["SerpApi:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            logger.LogInformation("SerpAPI key not configured — skipping Indeed");
            return [];
        }

        var query    = BuildQuery(profile);
        var location = !string.IsNullOrWhiteSpace(profile.LocationPreference) ? profile.LocationPreference : "United States";
        var jobs     = new List<Job>();
        const int maxPages = 3;

        for (int page = 0; page < maxPages; page++)
        {
            try
            {
                var url = "https://serpapi.com/search.json" +
                          $"?engine=indeed" +
                          $"&q={Uri.EscapeDataString(query)}" +
                          $"&l={Uri.EscapeDataString(location)}" +
                          $"&start={page * 10}" +
                          $"&api_key={apiKey}";

                var response = await http.GetFromJsonAsync<IndeedResponse>(url, ct);
                var listings = response?.JobsResults;
                if (listings is null || listings.Length == 0) break;

                foreach (var r in listings)
                {
                    var locType = r.Location?.Contains("Remote", StringComparison.OrdinalIgnoreCase) == true
                        ? LocationType.Remote : LocationType.OnSite;

                    var jobType = ParseJobType(r.DetectedExtensions?.ScheduleType);

                    jobs.Add(new Job
                    {
                        Id           = Guid.NewGuid(),
                        ExternalId   = r.JobId ?? Guid.NewGuid().ToString(),
                        Title        = r.Title ?? "Unknown",
                        Company      = r.CompanyName ?? "Unknown",
                        Location     = r.Location,
                        LocationType = locType,
                        JobType      = jobType,
                        Description  = r.Description ?? "",
                        Tags         = "[]",
                        Salary       = r.DetectedExtensions?.Salary,
                        PostedAt     = ParsePostedAt(r.DetectedExtensions?.PostedAt),
                        DiscoveredAt = DateTime.UtcNow,
                        Source       = JobSource.Indeed,
                        SourceUrl    = r.Link ?? "",
                        IsActive     = true
                    });
                }

                if (listings.Length < 10) break;
                await Task.Delay(1000, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Indeed (SerpAPI) page {Page} failed", page);
                break;
            }
        }

        logger.LogInformation("Indeed (SerpAPI): fetched {Count} jobs", jobs.Count);
        return jobs;
    }

    private static string BuildQuery(SearchProfile profile)
    {
        if (profile.SearchKeywords.Count > 0)
        {
            var kw = string.Join(" ", profile.SearchKeywords);
            return kw[..Math.Min(kw.Length, 100)];
        }
        var text = $"{profile.Name} {profile.Description}".Trim();
        return string.IsNullOrWhiteSpace(text) ? "software engineer" : text[..Math.Min(text.Length, 100)];
    }

    private static JobType ParseJobType(string? scheduleType) => scheduleType?.ToLowerInvariant() switch
    {
        "part-time" => JobType.PartTime,
        "contract"  => JobType.Contract,
        "freelance" => JobType.Freelance,
        _           => JobType.FullTime
    };

    private static DateTime? ParsePostedAt(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (DateTime.TryParse(raw, out var dt)) return dt;
        return null;
    }

    private record IndeedResponse(
        [property: JsonPropertyName("jobs_results")] IndeedJob[]? JobsResults);

    private record IndeedJob(
        [property: JsonPropertyName("job_id")]        string? JobId,
        [property: JsonPropertyName("title")]         string? Title,
        [property: JsonPropertyName("company_name")]  string? CompanyName,
        [property: JsonPropertyName("location")]      string? Location,
        [property: JsonPropertyName("description")]   string? Description,
        [property: JsonPropertyName("link")]          string? Link,
        [property: JsonPropertyName("detected_extensions")] IndeedExtensions? DetectedExtensions);

    private record IndeedExtensions(
        [property: JsonPropertyName("salary")]        string? Salary,
        [property: JsonPropertyName("schedule_type")] string? ScheduleType,
        [property: JsonPropertyName("posted_at")]     string? PostedAt);
}
