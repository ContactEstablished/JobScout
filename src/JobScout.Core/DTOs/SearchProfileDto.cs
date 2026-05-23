using JobScout.Core.Enums;

namespace JobScout.Core.DTOs;

public class SearchProfileDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ResumeFileName { get; set; }
    public string? LinkedInUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; }

    // Phase 2
    public List<string> SearchKeywords { get; set; } = [];
    public List<JobSource> PreferredSources { get; set; } = [];
    public List<string> PreferredJobTypes { get; set; } = [];
    public List<string> PreferredLocationTypes { get; set; } = [];
    public string? LocationPreference { get; set; }
    public List<string> DetectedSkills { get; set; } = [];
    public string? ProfileColor { get; set; }

    // Phase 5
    public string? PreferredModel { get; set; }
    public decimal? DesiredSalaryMin { get; set; }
    public decimal? DesiredSalaryMax { get; set; }
}

public class CreateProfileRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LinkedInUrl { get; set; }

    // Phase 2 — optional at creation, populated during wizard steps
    public List<string> SearchKeywords { get; set; } = [];
    public List<JobSource> PreferredSources { get; set; } = [];
    public List<string> PreferredJobTypes { get; set; } = [];
    public List<string> PreferredLocationTypes { get; set; } = [];
    public string? LocationPreference { get; set; }
    public string? ProfileColor { get; set; }

    // Phase 5
    public string? PreferredModel { get; set; }
    public decimal? DesiredSalaryMin { get; set; }
    public decimal? DesiredSalaryMax { get; set; }
}

public class UpdateProfileRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LinkedInUrl { get; set; }
    public bool IsActive { get; set; }

    // Phase 2
    public List<string> SearchKeywords { get; set; } = [];
    public List<JobSource> PreferredSources { get; set; } = [];
    public List<string> PreferredJobTypes { get; set; } = [];
    public List<string> PreferredLocationTypes { get; set; } = [];
    public string? LocationPreference { get; set; }
    public string? ProfileColor { get; set; }

    // Phase 5
    public string? PreferredModel { get; set; }
    public decimal? DesiredSalaryMin { get; set; }
    public decimal? DesiredSalaryMax { get; set; }
}

public class UpdateSkillsRequest
{
    public List<string> Skills { get; set; } = [];
}
