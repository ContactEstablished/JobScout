using JobScout.Core.Enums;

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

    // Phase 2: Search preferences
    public List<string> SearchKeywords { get; set; } = [];
    public List<JobSource> PreferredSources { get; set; } = [];
    public List<string> PreferredJobTypes { get; set; } = [];
    public List<string> PreferredLocationTypes { get; set; } = [];
    public string? LocationPreference { get; set; }
    public List<string> DetectedSkills { get; set; } = [];
    public string? ProfileColor { get; set; }

    public ICollection<Job> Jobs { get; set; } = [];
    public ICollection<AiScore> AiScores { get; set; } = [];
    public ICollection<UserRating> UserRatings { get; set; } = [];
    public ICollection<DailyMetric> DailyMetrics { get; set; } = [];
}
