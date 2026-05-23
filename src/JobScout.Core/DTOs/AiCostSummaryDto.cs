namespace JobScout.Core.DTOs;

public class AiCostSummaryDto
{
    public Guid? ProfileId { get; set; }
    public int ScoredJobCount { get; set; }
    public int TotalInputTokens { get; set; }
    public int TotalOutputTokens { get; set; }
    public decimal TotalCostUsd { get; set; }
    public decimal AverageCostPerJobUsd { get; set; }
    public List<AiCostByModelDto> ByModel { get; set; } = [];
}

public class AiCostByModelDto
{
    public string Model { get; set; } = string.Empty;
    public int ScoredJobCount { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal CostUsd { get; set; }
}
