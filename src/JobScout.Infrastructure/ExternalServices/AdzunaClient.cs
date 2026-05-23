using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobScout.Core.Enums;
using JobScout.Core.Interfaces;
using JobScout.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JobScout.Infrastructure.ExternalServices;

public class AdzunaClient(
    HttpClient http,
    IConfiguration config,
    ILogger<AdzunaClient> logger) : IJobBoardClient
{
    public JobSource Source => JobSource.Adzuna;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<IReadOnlyList<Job>> FetchJobsAsync(SearchProfile profile, CancellationToken ct = default)
    {
        var appId  = config["Adzuna:AppId"];
        var appKey = config["Adzuna:AppKey"];

        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(appKey))
        {
            logger.LogInformation("Adzuna credentials not configured — skipping");
            return [];
        }

        var query   = BuildQuery(profile);
        var jobs    = new List<Job>();
        const int perPage = 20;
        const int maxPages = 3;

        for (int page = 1; page <= maxPages; page++)
        {
            try
            {
                var url = $"https://api.adzuna.com/v1/api/jobs/us/search/{page}" +
                          $"?app_id={appId}&app_key={appKey}" +
                          $"&results_per_page={perPage}" +
                          $"&what={Uri.EscapeDataString(query)}" +
                          $"&content-type=application/json";

                var response = await http.GetFromJsonAsync<AdzunaResponse>(url, _json, ct);
                if (response?.Results is null || response.Results.Length == 0) break;

                foreach (var r in response.Results)
                {
                    jobs.Add(new Job
                    {
                        Id           = Guid.NewGuid(),
                        ExternalId   = r.Id,
                        Title        = r.Title,
                        Company      = r.Company?.DisplayName ?? "Unknown",
                        Location     = r.Location?.DisplayName,
                        LocationType = LocationType.OnSite,
                        JobType      = ParseJobType(r.ContractType),
                        Description  = r.Description ?? "",
                        Tags         = "[]",
                        PostedAt     = DateTime.TryParse(r.Created, out var d) ? d : null,
                        DiscoveredAt = DateTime.UtcNow,
                        Source       = JobSource.Adzuna,
                        SourceUrl    = r.RedirectUrl ?? "",
                        IsActive     = true
                    });
                }

                if (response.Results.Length < perPage) break;
                await Task.Delay(500, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Adzuna page {Page} failed", page);
                break;
            }
        }

        logger.LogInformation("Adzuna: fetched {Count} jobs", jobs.Count);
        return jobs;
    }

    private static string BuildQuery(SearchProfile profile)
    {
        if (profile.SearchKeywords.Count > 0)
            return string.Join(" ", profile.SearchKeywords)[..Math.Min(string.Join(" ", profile.SearchKeywords).Length, 80)];

        var text = $"{profile.Name} {profile.Description}".Trim();
        return string.IsNullOrWhiteSpace(text) ? "software engineer" : text[..Math.Min(text.Length, 80)];
    }

    private static JobType ParseJobType(string? contract) => contract?.ToLower() switch
    {
        "permanent" => JobType.FullTime,
        "contract"  => JobType.Contract,
        "part_time" => JobType.PartTime,
        _           => JobType.FullTime
    };

    private record AdzunaResponse(
        [property: JsonPropertyName("results")] AdzunaJob[] Results);

    private record AdzunaJob(
        [property: JsonPropertyName("id")]           string Id,
        [property: JsonPropertyName("title")]        string Title,
        [property: JsonPropertyName("company")]      AdzunaCompany? Company,
        [property: JsonPropertyName("location")]     AdzunaLocation? Location,
        [property: JsonPropertyName("description")]  string? Description,
        [property: JsonPropertyName("created")]      string? Created,
        [property: JsonPropertyName("redirect_url")] string? RedirectUrl,
        [property: JsonPropertyName("contract_type")] string? ContractType);

    private record AdzunaCompany(
        [property: JsonPropertyName("display_name")] string? DisplayName);

    private record AdzunaLocation(
        [property: JsonPropertyName("display_name")] string? DisplayName);
}
