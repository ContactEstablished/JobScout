namespace JobScout.Core.DTOs;

public class AiScoreDto
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid ProfileId { get; set; }
    public decimal Score { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public string MatchedKeywords { get; set; } = "[]";
    public DateTime ScoredAt { get; set; }
    public string ModelVersion { get; set; } = string.Empty;
}
