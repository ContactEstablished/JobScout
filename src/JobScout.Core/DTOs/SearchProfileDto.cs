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
}

public class CreateProfileRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LinkedInUrl { get; set; }
}

public class UpdateProfileRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LinkedInUrl { get; set; }
    public bool IsActive { get; set; }
}
