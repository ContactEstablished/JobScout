using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using JobScout.Core.Enums;
using JobScout.Core.Interfaces;
using JobScout.Core.Models;
using JobScout.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobScout.Infrastructure.ExternalServices;

public class CustomSourceClient(
    HttpClient http,
    JobScoutDbContext db,
    ILogger<CustomSourceClient> logger) : IJobBoardClient
{
    public JobSource Source => JobSource.Custom;

    public async Task<IReadOnlyList<Job>> FetchJobsAsync(SearchProfile profile, CancellationToken ct = default)
    {
        var sources = await db.CustomJobSources
            .Where(s => s.ProfileId == profile.Id && s.IsActive)
            .ToListAsync(ct);

        if (sources.Count == 0) return [];

        var jobs = new List<Job>();

        foreach (var source in sources)
        {
            try
            {
                var fetched = source.Format switch
                {
                    FeedFormat.Json => await FetchJsonAsync(source, ct),
                    _               => await FetchXmlAsync(source, ct)   // Rss + Atom
                };
                jobs.AddRange(fetched);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Custom source '{Name}' ({Url}) failed", source.Name, source.FeedUrl);
            }
        }

        logger.LogInformation("Custom sources: fetched {Count} jobs for profile {ProfileId}", jobs.Count, profile.Id);
        return jobs;
    }

    private async Task<IReadOnlyList<Job>> FetchXmlAsync(CustomJobSource source, CancellationToken ct)
    {
        var xml   = await http.GetStringAsync(source.FeedUrl, ct);
        var doc   = XDocument.Parse(xml);
        var items = doc.Descendants("item");           // RSS
        if (!items.Any()) items = doc.Descendants("entry"); // Atom

        var jobs = new List<Job>();
        XNamespace dc = "http://purl.org/dc/elements/1.1/";

        foreach (var item in items)
        {
            var title      = item.Element("title")?.Value ?? "Unknown";
            var link       = item.Element("link")?.Value
                          ?? item.Elements("link").FirstOrDefault(l => l.Attribute("rel")?.Value == "alternate")?.Attribute("href")?.Value
                          ?? "";
            var desc       = StripHtml(item.Element("description")?.Value ?? item.Element("summary")?.Value ?? "");
            var company    = item.Element(dc + "creator")?.Value ?? source.Name;
            var pubDateStr = item.Element("pubDate")?.Value ?? item.Element("updated")?.Value;

            DateTime.TryParse(pubDateStr, out var postedAt);

            jobs.Add(new Job
            {
                Id           = Guid.NewGuid(),
                ExternalId   = StableId(title, company, link),
                Title        = title,
                Company      = company,
                Location     = null,
                LocationType = LocationType.OnSite,
                JobType      = JobType.FullTime,
                Description  = desc,
                Tags         = "[]",
                PostedAt     = pubDateStr is null ? null : postedAt,
                DiscoveredAt = DateTime.UtcNow,
                Source       = JobSource.Custom,
                SourceUrl    = link,
                IsActive     = true
            });
        }

        return jobs;
    }

    private async Task<IReadOnlyList<Job>> FetchJsonAsync(CustomJobSource source, CancellationToken ct)
    {
        var json = await http.GetStringAsync(source.FeedUrl, ct);
        using var doc = JsonDocument.Parse(json);

        // Navigate to the jobs array using the configured path (e.g. "results.jobs")
        JsonElement root = doc.RootElement;
        if (!string.IsNullOrWhiteSpace(source.JsonJobsPath))
        {
            foreach (var segment in source.JsonJobsPath.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!root.TryGetProperty(segment, out root)) return [];
            }
        }

        if (root.ValueKind != JsonValueKind.Array) return [];

        var titleField = source.JsonTitleField       ?? "title";
        var compField  = source.JsonCompanyField      ?? "company";
        var locField   = source.JsonLocationField     ?? "location";
        var descField  = source.JsonDescriptionField  ?? "description";
        var urlField   = source.JsonUrlField          ?? "url";
        var dateField  = source.JsonPostedAtField     ?? "posted_at";

        var jobs = new List<Job>();

        foreach (var element in root.EnumerateArray())
        {
            var title   = GetString(element, titleField) ?? "Unknown";
            var company = GetString(element, compField)  ?? source.Name;
            var url     = GetString(element, urlField)   ?? "";
            var desc    = GetString(element, descField)  ?? "";
            var loc     = GetString(element, locField);
            var dateStr = GetString(element, dateField);

            DateTime.TryParse(dateStr, out var postedAt);
            var locType = loc?.Contains("Remote", StringComparison.OrdinalIgnoreCase) == true
                ? LocationType.Remote : LocationType.OnSite;

            jobs.Add(new Job
            {
                Id           = Guid.NewGuid(),
                ExternalId   = StableId(title, company, url),
                Title        = title,
                Company      = company,
                Location     = loc,
                LocationType = locType,
                JobType      = JobType.FullTime,
                Description  = StripHtml(desc),
                Tags         = "[]",
                PostedAt     = dateStr is null ? null : postedAt,
                DiscoveredAt = DateTime.UtcNow,
                Source       = JobSource.Custom,
                SourceUrl    = url,
                IsActive     = true
            });
        }

        return jobs;
    }

    private static string? GetString(JsonElement el, string field) =>
        el.TryGetProperty(field, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static string StripHtml(string html) =>
        Regex.Replace(html, "<[^>]*>", " ")
             .Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">").Trim();

    private static string StableId(string title, string company, string url)
    {
        var raw   = $"{title}|{company}|{url}";
        var hash  = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
