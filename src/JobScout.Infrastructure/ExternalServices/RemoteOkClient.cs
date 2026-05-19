using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobScout.Core.Enums;
using JobScout.Core.Interfaces;
using JobScout.Core.Models;
using Microsoft.Extensions.Logging;

namespace JobScout.Infrastructure.ExternalServices;

public class RemoteOkClient(HttpClient http, ILogger<RemoteOkClient> logger) : IJobBoardClient
{
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<IReadOnlyList<Job>> FetchJobsAsync(SearchProfile profile, CancellationToken ct = default)
    {
        try
        {
            // First element is a legal disclaimer object — skip it
            var raw = await http.GetFromJsonAsync<JsonElement[]>("https://remoteok.com/api", _json, ct);
            if (raw is null || raw.Length < 2) return [];

            var keywords = ExtractKeywords(profile);
            var jobs = new List<Job>();

            foreach (var el in raw.Skip(1))
            {
                if (!el.TryGetProperty("id", out var idEl)) continue;
                var externalId = idEl.GetString() ?? idEl.GetInt64().ToString();

                var tags = el.TryGetProperty("tags", out var tagsEl)
                    ? tagsEl.Deserialize<string[]>(_json) ?? []
                    : [];

                if (!tags.Any(t => keywords.Contains(t.ToLower()))) continue;

                var title    = el.TryGetProperty("position", out var pos) ? pos.GetString() ?? "Unknown" : "Unknown";
                var company  = el.TryGetProperty("company", out var co)   ? co.GetString()  ?? "Unknown" : "Unknown";
                var desc     = el.TryGetProperty("description", out var d) ? StripHtml(d.GetString() ?? "") : "";
                var url      = el.TryGetProperty("url", out var u)         ? u.GetString()  ?? "" : "";
                var dateStr  = el.TryGetProperty("date", out var dt)       ? dt.GetString()  : null;
                var location = el.TryGetProperty("location", out var loc)  ? loc.GetString() ?? "Remote" : "Remote";

                DateTime? postedAt = dateStr is not null && DateTime.TryParse(dateStr, out var parsed) ? parsed : null;

                jobs.Add(new Job
                {
                    Id           = Guid.NewGuid(),
                    ExternalId   = externalId,
                    Title        = title,
                    Company      = company,
                    Location     = location,
                    LocationType = LocationType.Remote,
                    JobType      = JobType.FullTime,
                    Description  = desc,
                    Tags         = JsonSerializer.Serialize(tags),
                    PostedAt     = postedAt,
                    DiscoveredAt = DateTime.UtcNow,
                    Source       = JobSource.RemoteOK,
                    SourceUrl    = url,
                    IsActive     = true
                });
            }

            logger.LogInformation("RemoteOK: fetched {Count} matching jobs", jobs.Count);
            return jobs;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RemoteOK fetch failed");
            return [];
        }
    }

    private static HashSet<string> ExtractKeywords(SearchProfile profile)
    {
        var text = $"{profile.Name} {profile.Description} {profile.ResumeText}".ToLower();
        var defaults = new[] { "dotnet", ".net", "csharp", "c#", "azure", "backend", "api", "fullstack" };
        var found = defaults.Where(k => text.Contains(k)).ToHashSet();
        if (found.Count == 0) found = defaults.ToHashSet();
        return found;
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;
        var result = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        return System.Text.RegularExpressions.Regex.Replace(result, @"\s+", " ").Trim();
    }
}
