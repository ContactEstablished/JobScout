using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobScout.Core.Interfaces;
using JobScout.Core.Models;
using JobScout.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace JobScout.Infrastructure.AI;

public class ClaudeAiScoringService(
    HttpClient http,
    JobScoutDbContext db,
    IConfiguration config,
    ILogger<ClaudeAiScoringService> logger) : IAiScoringService
{
    private const string Model = "claude-haiku-4-5-20251001";
    private const int MaxConcurrency = 3;

    public async Task<AiScore> ScoreJobAsync(Job job, SearchProfile profile)
    {
        var apiKey = config["Anthropic:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            logger.LogWarning("Anthropic API key not configured — returning default score");
            return DefaultScore(job, profile);
        }

        var prompt = BuildPrompt(job, profile);
        var raw    = await CallClaudeAsync(apiKey, prompt);
        return ParseResponse(raw, job, profile);
    }

    public async Task<IReadOnlyList<AiScore>> BatchScoreAsync(
        IEnumerable<Job> jobs, SearchProfile profile)
    {
        var apiKey = config["Anthropic:ApiKey"];
        var jobList = jobs.ToList();
        var scores = new List<AiScore>();
        var sem = new SemaphoreSlim(MaxConcurrency);

        var tasks = jobList.Select(async job =>
        {
            await sem.WaitAsync();
            try
            {
                // Skip if already scored for this profile
                var alreadyScored = await db.AiScores
                    .AnyAsync(s => s.JobId == job.Id && s.ProfileId == profile.Id);
                if (alreadyScored) return;

                var score = string.IsNullOrEmpty(apiKey)
                    ? DefaultScore(job, profile)
                    : await ScoreJobAsync(job, profile);

                lock (scores) scores.Add(score);
                db.AiScores.Add(score);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scoring failed for job {JobId}", job.Id);
            }
            finally
            {
                sem.Release();
            }
        });

        await Task.WhenAll(tasks);
        await db.SaveChangesAsync();

        logger.LogInformation("Batch scoring: {Count} jobs scored for profile {ProfileId}",
            scores.Count, profile.Id);

        return scores;
    }

    public async Task RecalibrateAsync(Guid profileId, bool resetHistory)
    {
        if (resetHistory)
        {
            var old = await db.AiScores.Where(s => s.ProfileId == profileId).ToListAsync();
            db.AiScores.RemoveRange(old);
            await db.SaveChangesAsync();
            logger.LogInformation("Removed {Count} existing scores for profile {ProfileId}",
                old.Count, profileId);
        }

        var profile = await db.SearchProfiles.FindAsync(profileId);
        if (profile is null) return;

        // Get rating examples for calibration
        var ratedJobs = await db.UserRatings
            .Where(r => r.ProfileId == profileId)
            .Include(r => r.Job)
            .ToListAsync();

        // Fetch jobs not yet scored for this profile
        var unscoredJobs = await db.Jobs
            .Where(j => j.IsActive && !j.AiScores.Any(s => s.ProfileId == profileId))
            .ToListAsync();

        if (unscoredJobs.Count == 0)
        {
            logger.LogInformation("No unscored jobs found for profile {ProfileId}", profileId);
            return;
        }

        logger.LogInformation("Recalibrating {Count} jobs for profile {ProfileId}",
            unscoredJobs.Count, profileId);

        await BatchScoreAsync(unscoredJobs, profile);
    }

    private async Task<string> CallClaudeAsync(string apiKey, string prompt)
    {
        var request = new
        {
            model = Model,
            max_tokens = 512,
            messages = new[] { new { role = "user", content = prompt } }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        req.Headers.Add("x-api-key", apiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");
        req.Content = new StringContent(
            JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        var response = await http.SendAsync(req);
        response.EnsureSuccessStatusCode();

        var body    = await response.Content.ReadFromJsonAsync<AnthropicResponse>();
        return body?.Content?.FirstOrDefault()?.Text ?? "";
    }

    private AiScore ParseResponse(string raw, Job job, SearchProfile profile)
    {
        // Extract JSON from the response (Claude may wrap it in prose)
        var start = raw.IndexOf('{');
        var end   = raw.LastIndexOf('}');

        if (start >= 0 && end > start)
        {
            try
            {
                var json    = raw[start..(end + 1)];
                var parsed  = JsonSerializer.Deserialize<ScoreResult>(json, _jsonOptions);

                if (parsed is not null)
                {
                    return new AiScore
                    {
                        Id             = Guid.NewGuid(),
                        JobId          = job.Id,
                        ProfileId      = profile.Id,
                        Score          = Math.Clamp(parsed.Score, 1m, 10m),
                        Reasoning      = parsed.Reasoning ?? "",
                        MatchedKeywords = JsonSerializer.Serialize(parsed.MatchedKeywords ?? []),
                        ScoredAt       = DateTime.UtcNow,
                        ModelVersion   = Model
                    };
                }
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to parse score JSON for job {JobId} — using default", job.Id);
            }
        }

        return DefaultScore(job, profile);
    }

    private static AiScore DefaultScore(Job job, SearchProfile profile) => new()
    {
        Id             = Guid.NewGuid(),
        JobId          = job.Id,
        ProfileId      = profile.Id,
        Score          = 5.0m,
        Reasoning      = "Score unavailable (API not configured or parse error).",
        MatchedKeywords = "[]",
        ScoredAt       = DateTime.UtcNow,
        ModelVersion   = "default"
    };

    private static string BuildPrompt(Job job, SearchProfile profile)
    {
        var resume = string.IsNullOrWhiteSpace(profile.ResumeText)
            ? $"Profile: {profile.Name}. {profile.Description}"
            : profile.ResumeText[..Math.Min(profile.ResumeText.Length, 2000)];

        var desc = string.IsNullOrWhiteSpace(job.Description)
            ? $"{job.Title} at {job.Company}"
            : job.Description[..Math.Min(job.Description.Length, 2000)];

        return $$"""
            You are a senior technical recruiter. Evaluate how well this candidate fits this job.

            CANDIDATE RESUME:
            {{resume}}

            JOB POSTING:
            Title: {{job.Title}}
            Company: {{job.Company}}
            Location: {{job.Location}} ({{job.LocationType}})
            Type: {{job.JobType}}
            Description: {{desc}}

            Return ONLY a JSON object (no markdown, no explanation outside the JSON):
            {
              "score": <number 1.0-10.0>,
              "reasoning": "<2-3 sentence explanation of fit>",
              "matchedKeywords": ["<keyword1>", "<keyword2>"],
              "redFlags": ["<concern1>"]
            }
            """;
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private record ScoreResult(
        [property: JsonPropertyName("score")]           decimal Score,
        [property: JsonPropertyName("reasoning")]       string? Reasoning,
        [property: JsonPropertyName("matchedKeywords")] string[]? MatchedKeywords,
        [property: JsonPropertyName("redFlags")]        string[]? RedFlags);

    private record AnthropicResponse(
        [property: JsonPropertyName("content")] AnthropicContentBlock[]? Content);

    private record AnthropicContentBlock(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("text")] string? Text);
}
