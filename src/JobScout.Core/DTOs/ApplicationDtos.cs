using JobScout.Core.Enums;

namespace JobScout.Core.DTOs;

public class CreateApplicationRequest
{
    public Guid JobId { get; set; }
    public Guid ProfileId { get; set; }
    public string? Notes { get; set; }
}

public class UpdateStatusRequest
{
    public ApplicationStatus Status { get; set; }
    public string? Notes { get; set; }
}

public class ApplicationDto
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid ProfileId { get; set; }
    public DateTime AppliedAt { get; set; }
    public ApplicationStatus Status { get; set; }
    public string? Notes { get; set; }
    public List<StatusChangeDto> StatusHistory { get; set; } = [];
    public JobSummaryDto? Job { get; set; }
}

public class StatusChangeDto
{
    public ApplicationStatus Status { get; set; }
    public DateTime ChangedAt { get; set; }
    public string? Notes { get; set; }
}

public class PipelineDto
{
    public int Applied { get; set; }
    public int Interviewing { get; set; }
    public int Offered { get; set; }
    public int Accepted { get; set; }
    public int Rejected { get; set; }
    public int Withdrawn { get; set; }
}
