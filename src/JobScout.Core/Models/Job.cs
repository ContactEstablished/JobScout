using JobScout.Core.Enums;

namespace JobScout.Core.Models;

public class Job
{
    public Guid Id { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string? Location { get; set; }
    public LocationType LocationType { get; set; }
    public JobType JobType { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Tags { get; set; } = "[]";
    public string? Salary { get; set; }
    public DateTime? PostedAt { get; set; }
    public DateTime DiscoveredAt { get; set; }
    public JobSource Source { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<AiScore> AiScores { get; set; } = [];
    public ICollection<UserRating> UserRatings { get; set; } = [];
}
