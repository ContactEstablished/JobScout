using JobScout.Core.Enums;

namespace JobScout.Core.DTOs;

public class JobSummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string? Location { get; set; }
    public LocationType LocationType { get; set; }
    public JobType JobType { get; set; }
    public string Tags { get; set; } = "[]";
    public string? Salary { get; set; }
    public DateTime? PostedAt { get; set; }
    public DateTime DiscoveredAt { get; set; }
    public JobSource Source { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public bool IsPotentialDuplicate { get; set; }
    public Guid? DuplicateOfJobId { get; set; }
    public AiScoreDto? AiScore { get; set; }
    public UserRatingDto? UserRating { get; set; }
}
