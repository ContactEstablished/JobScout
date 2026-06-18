using JobScout.Core.Enums;

namespace JobScout.Core.DTOs;

public class FilterPresetDto
{
    public Guid Id { get; set; }
    public Guid ProfileId { get; set; }
    public string Name { get; set; } = string.Empty;
    public JobSource? Source { get; set; }
    public decimal? MinScore { get; set; }
    public LocationType? LocationType { get; set; }
    public JobType? JobType { get; set; }
    public string? Query { get; set; }
    public JobSortBy SortBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SaveFilterPresetRequest
{
    public Guid ProfileId { get; set; }
    public string Name { get; set; } = string.Empty;
    public JobSource? Source { get; set; }
    public decimal? MinScore { get; set; }
    public LocationType? LocationType { get; set; }
    public JobType? JobType { get; set; }
    public string? Query { get; set; }
    public JobSortBy SortBy { get; set; } = JobSortBy.AiScore;
}
