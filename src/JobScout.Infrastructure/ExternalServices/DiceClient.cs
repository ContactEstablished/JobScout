using System.Text.RegularExpressions;
using System.Xml.Linq;
using JobScout.Core.Enums;
using JobScout.Core.Interfaces;
using JobScout.Core.Models;
using Microsoft.Extensions.Logging;

namespace JobScout.Infrastructure.ExternalServices;

public class DiceClient(
    HttpClient http,
    ILogger<DiceClient> logger) : IJobBoardClient
{
    public JobSource Source => JobSource.Dice;

    public async Task<IReadOnlyList<Job>> FetchJobsAsync(SearchProfile profile, CancellationToken ct = default)
    {
        var keyword = BuildQuery(profile);
        var jobs    = new List<Job>();

        // Dice exposes an RSS feed for job searches
        var url = $"https://www.dice.com/jobs/q-{Uri.EscapeDataString(keyword)}-jobs.rss";

        try
        {
            var xml  = await http.GetStringAsync(url, ct);
            var doc  = XDocument.Parse(xml);
            XNamespace dc = "http://purl.org/dc/elements/1.1/";

            var items = doc.Descendants("item");
            foreach (var item in items)
            {
                var title      = item.Element("title")?.Value ?? "Unknown";
                var link       = item.Element("link")?.Value ?? "";
                var desc       = StripHtml(item.Element("description")?.Value ?? "");
                var company    = item.Element(dc + "creator")?.Value
                              ?? item.Element("author")?.Value
                              ?? "Unknown";
                var pubDateStr = item.Element("pubDate")?.Value;
                var location   = item.Element(XNamespace.Get("https://www.dice.com/") + "location")?.Value
                              ?? ExtractLocation(desc);

                DateTime.TryParse(pubDateStr, out var postedAt);

                var externalId = GenerateId(title, company, link);
                var locType    = location?.Contains("Remote", StringComparison.OrdinalIgnoreCase) == true
                    ? LocationType.Remote : LocationType.OnSite;

                jobs.Add(new Job
                {
                    Id           = Guid.NewGuid(),
                    ExternalId   = externalId,
                    Title        = title,
                    Company      = company,
                    Location     = location,
                    LocationType = locType,
                    JobType      = JobType.FullTime,
                    Description  = desc,
                    Tags         = "[]",
                    PostedAt     = pubDateStr is null ? null : postedAt,
                    DiscoveredAt = DateTime.UtcNow,
                    Source       = JobSource.Dice,
                    SourceUrl    = link,
                    IsActive     = true
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Dice RSS fetch failed for keyword '{Keyword}'", keyword);
        }

        logger.LogInformation("Dice: fetched {Count} jobs", jobs.Count);
        return jobs;
    }

    private static string BuildQuery(SearchProfile profile)
    {
        if (profile.SearchKeywords.Count > 0)
            return string.Join("+", profile.SearchKeywords.Take(3));
        var text = profile.Name.Trim();
        return string.IsNullOrWhiteSpace(text) ? "software+developer" : text.Replace(" ", "+");
    }

    private static string StripHtml(string html) =>
        Regex.Replace(html, "<[^>]*>", " ").Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">").Trim();

    // Try to extract a location from the first line of the description
    private static string? ExtractLocation(string desc)
    {
        var firstLine = desc.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return firstLine?.Length < 80 ? firstLine : null;
    }

    private static string GenerateId(string title, string company, string url)
    {
        var raw   = $"{title}|{company}|{url}";
        var bytes = System.Text.Encoding.UTF8.GetBytes(raw);
        var hash  = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
