using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic.SDK.Extensions;
using Anthropic.SDK.Messaging;
using JobScout.Core.Interfaces;
using JobScout.Core.Models;
using JobScout.Infrastructure.Configuration;
using JobScout.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AnthropicTool = Anthropic.SDK.Common.Tool;
using AnthropicFunction = Anthropic.SDK.Common.Function;

namespace JobScout.Infrastructure.AI;

public class ClaudeAiScoringService(
    JobScoutDbContext db,
    IConfiguration config,
    ISecretStore secrets,
    IAnthropicClientFactory clientFactory,
    INotificationService notifications,
    ILogger<ClaudeAiScoringService> logger) : IAiScoringService
{
    private const string DefaultModel = "claude-haiku-4-5-20251001";
    private const int MaxConcurrency = 3;
    private const string ToolName = "submit_job_match_score";
    private const int FewShotRatingCount = 10;

    public async Task<AiScore> ScoreJobAsync(Job job, SearchProfile profile)
    {
        var apiKey = await secrets.GetAsync("Anthropic:ApiKey");
        if (string.IsNullOrEmpty(apiKey))
        {
            logger.LogWarning("Anthropic API key not configured — returning default score");
            return DefaultScore(job, profile);
        }

        var fewShotExamples = await GetFewShotExamplesAsync(profile.Id);
        return await ScoreInternalAsync(job, profile, apiKey, fewShotExamples);
    }

    public async Task<IReadOnlyList<AiScore>> BatchScoreAsync(
        IEnumerable<Job> jobs, SearchProfile profile)
    {
        var apiKey = await secrets.GetAsync("Anthropic:ApiKey");
        var jobList = jobs.ToList();
        var scores = new List<AiScore>();
        var sem = new SemaphoreSlim(MaxConcurrency);

        var fewShotExamples = string.IsNullOrEmpty(apiKey)
            ? []
            : await GetFewShotExamplesAsync(profile.Id);

        var tasks = jobList.Select(async job =>
        {
            await sem.WaitAsync();
            try
            {
                var alreadyScored = await db.AiScores
                    .AnyAsync(s => s.JobId == job.Id && s.ProfileId == profile.Id);
                if (alreadyScored) return;

                var score = string.IsNullOrEmpty(apiKey)
                    ? DefaultScore(job, profile)
                    : await ScoreInternalAsync(job, profile, apiKey, fewShotExamples);

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

        foreach (var score in scores.Where(s => s.Score >= 8m))
        {
            try
            {
                var job = jobList.FirstOrDefault(j => j.Id == score.JobId);
                if (job is not null)
                    await notifications.OnHighScoreCreatedAsync(score, job, profile);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to emit strong-fit notification for score {ScoreId}", score.Id);
            }
        }

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

    private async Task<AiScore> ScoreInternalAsync(
        Job job, SearchProfile profile, string apiKey, IReadOnlyList<RatingExample> fewShot)
    {
        var model = ResolveModel(profile);
        var messenger = clientFactory.Create(apiKey);

        var systemPrompt = BuildSystemPrompt(profile, fewShot);
        var userPrompt = BuildUserPrompt(job, profile);

        var tool = new AnthropicTool(
            new AnthropicFunction(ToolName, ScoreToolDescription, ScoreToolSchema));

        var parameters = new MessageParameters
        {
            Model = model,
            MaxTokens = 1024,
            Temperature = 0.0m,
            System = [new SystemMessage(systemPrompt)],
            Messages = [new Message(RoleType.User, userPrompt)],
            Tools = [tool],
            ToolChoice = new ToolChoice { Type = ToolChoiceType.Tool, Name = ToolName }
        };

        try
        {
            var response = await messenger.SendAsync(parameters);
            var toolUse = response.Content.OfType<ToolUseContent>().FirstOrDefault();

            if (toolUse?.Input is null)
            {
                logger.LogWarning("No tool_use returned for job {JobId} — using default", job.Id);
                return DefaultScore(job, profile);
            }

            var input = toolUse.Input;
            var score = BuildScoreFromToolInput(input, job, profile, model);

            if (response.Usage is { } usage)
            {
                score.InputTokens = usage.InputTokens;
                score.OutputTokens = usage.OutputTokens;
                try
                {
                    score.EstimatedCostUsd = response.CalculateCost().TotalCostUsd;
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Cost calculation skipped for model {Model}", model);
                }
            }

            return score;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Anthropic SDK call failed for job {JobId}", job.Id);
            return DefaultScore(job, profile);
        }
    }

    private static AiScore BuildScoreFromToolInput(
        JsonNode input, Job job, SearchProfile profile, string model)
    {
        decimal Read(string key, decimal fallback)
        {
            var node = input[key];
            if (node is null) return fallback;
            try { return Math.Clamp(node.GetValue<decimal>(), 0m, 10m); }
            catch { return fallback; }
        }

        string[] ReadArray(string key)
        {
            var node = input[key];
            if (node is JsonArray arr)
                return [.. arr.Select(n => n?.GetValue<string>() ?? "").Where(s => !string.IsNullOrWhiteSpace(s))];
            return [];
        }

        var overall = Read("score", 5m);
        var skills = Read("skillsMatch", overall);
        var experience = Read("experienceFit", overall);
        var culture = Read("cultureFit", overall);
        var compensation = Read("compensationFit", overall);
        var reasoning = input["reasoning"]?.GetValue<string>() ?? "";

        return new AiScore
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            ProfileId = profile.Id,
            Score = Math.Clamp(overall, 1m, 10m),
            Reasoning = reasoning,
            MatchedKeywords = JsonSerializer.Serialize(ReadArray("matchedKeywords")),
            GrowthAreas = JsonSerializer.Serialize(ReadArray("growthAreas")),
            RedFlags = JsonSerializer.Serialize(ReadArray("redFlags")),
            SkillsMatchScore = skills,
            ExperienceFitScore = experience,
            CultureFitScore = culture,
            CompensationFitScore = compensation,
            ScoredAt = DateTime.UtcNow,
            ModelVersion = model
        };
    }

    private static AiScore DefaultScore(Job job, SearchProfile profile) => new()
    {
        Id = Guid.NewGuid(),
        JobId = job.Id,
        ProfileId = profile.Id,
        Score = 5.0m,
        Reasoning = "Score unavailable (API not configured or error during scoring).",
        MatchedKeywords = "[]",
        GrowthAreas = "[]",
        RedFlags = "[]",
        ScoredAt = DateTime.UtcNow,
        ModelVersion = "default"
    };

    private string ResolveModel(SearchProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.PreferredModel))
            return profile.PreferredModel;

        var configured = config["Anthropic:Model"];
        return string.IsNullOrWhiteSpace(configured) ? DefaultModel : configured;
    }

    private async Task<IReadOnlyList<RatingExample>> GetFewShotExamplesAsync(Guid profileId)
    {
        var ratings = await db.UserRatings
            .Where(r => r.ProfileId == profileId)
            .Include(r => r.Job)
            .OrderByDescending(r => r.RatedAt)
            .Take(FewShotRatingCount)
            .ToListAsync();

        return [.. ratings
            .Where(r => r.Job is not null)
            .Select(r => new RatingExample(
                r.Job!.Title,
                r.Job.Company,
                r.Stars,
                Truncate(r.Notes, 200)))];
    }

    private static string BuildSystemPrompt(SearchProfile profile, IReadOnlyList<RatingExample> fewShot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a senior technical recruiter evaluating how well a candidate fits a job posting.");
        sb.AppendLine();
        sb.AppendLine("Score each dimension on a scale of 0.0 to 10.0:");
        sb.AppendLine("  - skillsMatch: overlap between candidate skills/experience and required skills.");
        sb.AppendLine("  - experienceFit: alignment of years of experience and seniority.");
        sb.AppendLine("  - cultureFit: industry, company size, mission alignment based on profile signals.");
        sb.AppendLine("  - compensationFit: compares posted salary against the candidate's desired range when provided.");
        sb.AppendLine("Overall `score` is a holistic 1.0–10.0 rating that takes all dimensions into account.");
        sb.AppendLine();
        sb.AppendLine("`matchedKeywords`: terms from the posting that align with the candidate's background.");
        sb.AppendLine("`growthAreas`: skills required by the posting that are NOT clearly present in the resume — frame as opportunities, not negatives.");
        sb.AppendLine("`redFlags`: serious mismatches, missing must-have requirements, or concerning patterns.");
        sb.AppendLine();
        sb.AppendLine("You MUST call the submit_job_match_score tool with your evaluation. Do not respond with prose.");

        if (profile.DesiredSalaryMin.HasValue || profile.DesiredSalaryMax.HasValue)
        {
            sb.AppendLine();
            sb.Append("Candidate desired salary range (USD): ");
            sb.Append(profile.DesiredSalaryMin.HasValue ? $"${profile.DesiredSalaryMin:N0}" : "open");
            sb.Append(" to ");
            sb.AppendLine(profile.DesiredSalaryMax.HasValue ? $"${profile.DesiredSalaryMax:N0}" : "open");
            sb.AppendLine("Penalize compensationFit when the posting is well below this range; reward when at or above.");
        }

        if (fewShot.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("CALIBRATION — the candidate previously rated these jobs (1=worst, 5=best). Use them to anchor your scoring style:");
            foreach (var ex in fewShot)
            {
                sb.Append($"  - \"{ex.Title}\" @ {ex.Company} — rated {ex.Stars}/5");
                if (!string.IsNullOrWhiteSpace(ex.Notes))
                    sb.Append($" (notes: {ex.Notes})");
                sb.AppendLine();
            }
            sb.AppendLine("Higher candidate ratings should correlate with higher overall scores from you.");
        }

        return sb.ToString();
    }

    private static string BuildUserPrompt(Job job, SearchProfile profile)
    {
        var resume = string.IsNullOrWhiteSpace(profile.ResumeText)
            ? $"Profile: {profile.Name}. {profile.Description}"
            : profile.ResumeText[..Math.Min(profile.ResumeText.Length, 4000)];

        var desc = string.IsNullOrWhiteSpace(job.Description)
            ? $"{job.Title} at {job.Company}"
            : job.Description[..Math.Min(job.Description.Length, 4000)];

        return $$"""
            CANDIDATE RESUME:
            {{resume}}

            JOB POSTING:
            Title: {{job.Title}}
            Company: {{job.Company}}
            Location: {{job.Location}} ({{job.LocationType}})
            Type: {{job.JobType}}
            Salary: {{(string.IsNullOrWhiteSpace(job.Salary) ? "not listed" : job.Salary)}}
            Description: {{desc}}

            Evaluate fit and call submit_job_match_score with all required fields.
            """;
    }

    private static string Truncate(string? text, int maxLen)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= maxLen ? text : text[..maxLen] + "…";
    }

    private const string ScoreToolDescription =
        "Submit a multi-dimensional match score for a candidate / job pairing. " +
        "Use this tool for every evaluation — do not return free-form text.";

    private const string ScoreToolSchema = """
        {
          "type": "object",
          "properties": {
            "score":           { "type": "number", "minimum": 1, "maximum": 10, "description": "Overall holistic 1-10 fit score." },
            "skillsMatch":     { "type": "number", "minimum": 0, "maximum": 10, "description": "Skill overlap, 0-10." },
            "experienceFit":   { "type": "number", "minimum": 0, "maximum": 10, "description": "Seniority/years alignment, 0-10." },
            "cultureFit":      { "type": "number", "minimum": 0, "maximum": 10, "description": "Industry/company/values alignment, 0-10." },
            "compensationFit": { "type": "number", "minimum": 0, "maximum": 10, "description": "Salary alignment vs. candidate range, 0-10. Use 5 if either is unknown." },
            "reasoning":       { "type": "string", "description": "2-3 sentence rationale for the overall score." },
            "matchedKeywords": { "type": "array", "items": { "type": "string" }, "description": "Resume-aligned terms from the posting." },
            "growthAreas":     { "type": "array", "items": { "type": "string" }, "description": "Skills the posting requires that are missing from the resume." },
            "redFlags":        { "type": "array", "items": { "type": "string" }, "description": "Serious mismatches or concerns." }
          },
          "required": ["score", "skillsMatch", "experienceFit", "cultureFit", "compensationFit", "reasoning"]
        }
        """;

    private record RatingExample(string Title, string Company, int Stars, string Notes);
}
