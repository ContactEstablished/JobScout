namespace JobScout.Core.Models;

public class AiScore
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid ProfileId { get; set; }
    public decimal Score { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public string MatchedKeywords { get; set; } = "[]";
    public DateTime ScoredAt { get; set; }
    public string ModelVersion { get; set; } = string.Empty;

    // Phase 5: multi-dimensional sub-scores
    public decimal? SkillsMatchScore { get; set; }
    public decimal? ExperienceFitScore { get; set; }
    public decimal? CultureFitScore { get; set; }
    public decimal? CompensationFitScore { get; set; }
    public string GrowthAreas { get; set; } = "[]";
    public string RedFlags { get; set; } = "[]";

    // Phase 5: cost tracking
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public decimal? EstimatedCostUsd { get; set; }

    public Job Job { get; set; } = null!;
    public SearchProfile Profile { get; set; } = null!;
}
