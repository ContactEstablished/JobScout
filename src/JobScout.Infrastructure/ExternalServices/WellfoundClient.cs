using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobScout.Core.Enums;
using JobScout.Core.Interfaces;
using JobScout.Core.Models;
using JobScout.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;

namespace JobScout.Infrastructure.ExternalServices;

public class WellfoundClient(
    HttpClient http,
    ISecretStore secrets,
    ILogger<WellfoundClient> logger) : IJobBoardClient
{
    public JobSource Source => JobSource.Wellfound;

    private const string GraphQlEndpoint = "https://wellfound.com/graphql";

    public async Task<IReadOnlyList<Job>> FetchJobsAsync(SearchProfile profile, CancellationToken ct = default)
    {
        var token = await secrets.GetAsync("Wellfound:AccessToken", ct);
        if (string.IsNullOrEmpty(token))
        {
            logger.LogInformation("Wellfound access token not configured — skipping");
            return [];
        }

        var query   = BuildQuery(profile);
        var jobs    = new List<Job>();

        var graphqlQuery = new
        {
            query = """
                query JobSearch($query: String!) {
                  jobListings(query: $query, first: 50) {
                    edges {
                      node {
                        id
                        title
                        description
                        jobType
                        locationNames
                        salary
                        applyUrl
                        createdAt
                        startup {
                          name
                        }
                      }
                    }
                  }
                }
                """,
            variables = new { query }
        };

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, GraphQlEndpoint)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(graphqlQuery),
                    Encoding.UTF8,
                    "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<WellfoundResponse>(cancellationToken: ct);
            var edges  = result?.Data?.JobListings?.Edges;
            if (edges is null) return jobs;

            foreach (var edge in edges)
            {
                var node    = edge.Node;
                if (node is null) continue;

                var location = node.LocationNames?.FirstOrDefault();
                var locType  = location?.Contains("Remote", StringComparison.OrdinalIgnoreCase) == true
                    ? LocationType.Remote : LocationType.OnSite;

                jobs.Add(new Job
                {
                    Id           = Guid.NewGuid(),
                    ExternalId   = node.Id ?? Guid.NewGuid().ToString(),
                    Title        = node.Title ?? "Unknown",
                    Company      = node.Startup?.Name ?? "Unknown",
                    Location     = location,
                    LocationType = locType,
                    JobType      = ParseJobType(node.JobType),
                    Description  = node.Description ?? "",
                    Tags         = "[]",
                    Salary       = node.Salary,
                    PostedAt     = node.CreatedAt,
                    DiscoveredAt = DateTime.UtcNow,
                    Source       = JobSource.Wellfound,
                    SourceUrl    = node.ApplyUrl ?? "",
                    IsActive     = true
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Wellfound GraphQL fetch failed");
        }

        logger.LogInformation("Wellfound: fetched {Count} jobs", jobs.Count);
        return jobs;
    }

    private static string BuildQuery(SearchProfile profile)
    {
        if (profile.SearchKeywords.Count > 0)
        {
            var kw = string.Join(" ", profile.SearchKeywords);
            return kw[..Math.Min(kw.Length, 100)];
        }
        var text = profile.Name.Trim();
        return string.IsNullOrWhiteSpace(text) ? "software engineer" : text[..Math.Min(text.Length, 100)];
    }

    private static JobType ParseJobType(string? raw) => raw?.ToLowerInvariant() switch
    {
        "part_time" => JobType.PartTime,
        "contract"  => JobType.Contract,
        "freelance" => JobType.Freelance,
        _           => JobType.FullTime
    };

    // Response shape
    private record WellfoundResponse(
        [property: JsonPropertyName("data")] WellfoundData? Data);

    private record WellfoundData(
        [property: JsonPropertyName("jobListings")] WellfoundListings? JobListings);

    private record WellfoundListings(
        [property: JsonPropertyName("edges")] WellfoundEdge[]? Edges);

    private record WellfoundEdge(
        [property: JsonPropertyName("node")] WellfoundNode? Node);

    private record WellfoundNode(
        [property: JsonPropertyName("id")]            string? Id,
        [property: JsonPropertyName("title")]         string? Title,
        [property: JsonPropertyName("description")]   string? Description,
        [property: JsonPropertyName("jobType")]       string? JobType,
        [property: JsonPropertyName("locationNames")] string[]? LocationNames,
        [property: JsonPropertyName("salary")]        string? Salary,
        [property: JsonPropertyName("applyUrl")]      string? ApplyUrl,
        [property: JsonPropertyName("createdAt")]     DateTime? CreatedAt,
        [property: JsonPropertyName("startup")]       WellfoundStartup? Startup);

    private record WellfoundStartup(
        [property: JsonPropertyName("name")] string? Name);
}
