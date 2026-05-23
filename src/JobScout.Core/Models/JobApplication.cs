using JobScout.Core.Enums;

namespace JobScout.Core.Models;

public class JobApplication
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid ProfileId { get; set; }
    public DateTime AppliedAt { get; set; }
    public ApplicationStatus Status { get; set; }
    public string? Notes { get; set; }
    public List<StatusChange> StatusHistory { get; set; } = [];

    // Navigation properties
    public Job Job { get; set; } = null!;
    public SearchProfile Profile { get; set; } = null!;
}

public class StatusChange
{
    public ApplicationStatus Status { get; set; }
    public DateTime ChangedAt { get; set; }
    public string? Notes { get; set; }
}
