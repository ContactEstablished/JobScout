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
}
