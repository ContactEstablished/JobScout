using JobScout.Core.Enums;

namespace JobScout.Core.DTOs;

public class CustomJobSourceDto
{
    public Guid Id { get; set; }
    public Guid ProfileId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FeedUrl { get; set; } = string.Empty;
    public FeedFormat Format { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? JsonJobsPath { get; set; }
    public string? JsonTitleField { get; set; }
    public string? JsonCompanyField { get; set; }
    public string? JsonLocationField { get; set; }
    public string? JsonDescriptionField { get; set; }
    public string? JsonUrlField { get; set; }
    public string? JsonPostedAtField { get; set; }
}

public class CreateCustomJobSourceRequest
{
    public Guid ProfileId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FeedUrl { get; set; } = string.Empty;
    public FeedFormat Format { get; set; }
    public string? JsonJobsPath { get; set; }
    public string? JsonTitleField { get; set; }
    public string? JsonCompanyField { get; set; }
    public string? JsonLocationField { get; set; }
    public string? JsonDescriptionField { get; set; }
    public string? JsonUrlField { get; set; }
    public string? JsonPostedAtField { get; set; }
}

public class ConfirmDuplicateRequest
{
    public Guid DuplicateJobId { get; set; }
}
