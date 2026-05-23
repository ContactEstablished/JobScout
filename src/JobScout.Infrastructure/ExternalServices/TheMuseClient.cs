using System.Net.Http.Json;
using System.Text.Json.Serialization;
using JobScout.Core.Enums;
using JobScout.Core.Interfaces;
using JobScout.Core.Models;
using Microsoft.Extensions.Logging;

namespace JobScout.Infrastructure.ExternalServices;

public class TheMuseClient(HttpClient http, ILogger<TheMuseClient> logger) : IJobBoardClient
{
    public JobSource Source => JobSource.TheMuse;

    private static readonly string[] DefaultCategories = ["Software Engineer", "Developer", "Data Science"];

    public async Task<IReadOnlyList<Job>> FetchJobsAsync(SearchProfile profile, CancellationToken ct = default)
    {
        var jobs = new List<Job>();
        var categories = profile.SearchKeywords.Count > 0
            ? profile.SearchKeywords.ToArray()
            : DefaultCategories;

        foreach (var category in categories)
        {
            for (int page = 0; page < 3; page++)
            {
                try
                {
                    var url = $"https://www.themuse.com/api/public/jobs" +
                              $"?category={Uri.EscapeDataString(category)}&page={page}&api_key=";

                    var response = await http.GetFromJsonAsync<TheMuseResponse>(url, ct);
                    if (response?.Results is null || response.Results.Length == 0) break;

                    foreach (var r in response.Results)
                    {
                        var location  = r.Locations?.FirstOrDefault()?.Name;
                        var locType   = string.IsNullOrEmpty(location) || location.Contains("Flexible") || location.Contains("Remote")
                            ? LocationType.Remote : LocationType.OnSite;
                        var levelName = r.Levels?.FirstOrDefault()?.Name ?? "";
                        var jobType   = levelName.Contains("Internship") ? JobType.PartTime : JobType.FullTime;

                        jobs.Add(new Job
                        {
                            Id           = Guid.NewGuid(),
                            ExternalId   = r.Id.ToString(),
                            Title        = r.Name,
                            Company      = r.Company?.Name ?? "Unknown",
                            Location     = location,
                            LocationType = locType,
                            JobType      = jobType,
                            Description  = "",
                            Tags         = "[]",
                            PostedAt     = DateTime.TryParse(r.PublicationDate, out var d) ? d : null,
                            DiscoveredAt = DateTime.UtcNow,
                            Source       = JobSource.TheMuse,
                            SourceUrl    = r.Refs?.LandingPage ?? "",
                            IsActive     = true
                        });
                    }

                    if (response.Results.Length < 20) break;
                    await Task.Delay(400, ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "The Muse page {Page} failed for category {Category}", page, category);
                    break;
                }
            }
        }

        logger.LogInformation("TheMuse: fetched {Count} jobs", jobs.Count);
        return jobs;
    }

    private record TheMuseResponse(
        [property: JsonPropertyName("results")] TheMuseJob[] Results);

    private record TheMuseJob(
        [property: JsonPropertyName("id")]               int Id,
        [property: JsonPropertyName("name")]             string Name,
        [property: JsonPropertyName("company")]          TheMuseCompany? Company,
        [property: JsonPropertyName("locations")]        TheMuseLocation[]? Locations,
        [property: JsonPropertyName("levels")]           TheMuseLevel[]? Levels,
        [property: JsonPropertyName("refs")]             TheMuseRefs? Refs,
        [property: JsonPropertyName("publication_date")] string? PublicationDate);

    private record TheMuseCompany(
        [property: JsonPropertyName("name")] string? Name);

    private record TheMuseLocation(
        [property: JsonPropertyName("name")] string? Name);

    private record TheMuseLevel(
        [property: JsonPropertyName("name")] string? Name);

    private record TheMuseRefs(
        [property: JsonPropertyName("landing_page")] string? LandingPage);
}
