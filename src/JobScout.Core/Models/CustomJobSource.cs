using JobScout.Core.Enums;

namespace JobScout.Core.Models;

public class CustomJobSource
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FeedUrl { get; set; } = string.Empty;
    public FeedFormat Format { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }

    // FK to SearchProfile
    public Guid ProfileId { get; set; }
    public SearchProfile Profile { get; set; } = null!;

    // JSON field mapping for Format = Json
    public string? JsonJobsPath { get; set; }
    public string? JsonTitleField { get; set; }
    public string? JsonCompanyField { get; set; }
    public string? JsonLocationField { get; set; }
    public string? JsonDescriptionField { get; set; }
    public string? JsonUrlField { get; set; }
    public string? JsonPostedAtField { get; set; }
}
