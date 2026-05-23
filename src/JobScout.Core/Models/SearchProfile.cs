namespace JobScout.Core.Models;

public class SearchProfile
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ResumeText { get; set; }
    public string? ResumeFileName { get; set; }
    public string? LinkedInUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; }

    public string UserId { get; set; } = string.Empty;

    public ICollection<Job> Jobs { get; set; } = [];
    public ICollection<AiScore> AiScores { get; set; } = [];
    public ICollection<UserRating> UserRatings { get; set; } = [];
    public ICollection<DailyMetric> DailyMetrics { get; set; } = [];
}
