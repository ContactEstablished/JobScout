using JobScout.Core.Enums;
using JobScout.Core.Models;

namespace JobScout.Infrastructure.Tests.Builders;

public class JobApplicationBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _jobId = Guid.NewGuid();
    private Guid _profileId = Guid.NewGuid();
    private ApplicationStatus _status = ApplicationStatus.Applied;
    private DateTime _appliedAt = DateTime.UtcNow;

    public JobApplicationBuilder ForJob(Guid jobId) { _jobId = jobId; return this; }
    public JobApplicationBuilder ForProfile(Guid profileId) { _profileId = profileId; return this; }
    public JobApplicationBuilder WithStatus(ApplicationStatus status) { _status = status; return this; }

    public JobApplication Build() => new()
    {
        Id = _id,
        JobId = _jobId,
        ProfileId = _profileId,
        Status = _status,
        AppliedAt = _appliedAt,
        StatusHistory =
        [
            new StatusChange { Status = ApplicationStatus.Applied, ChangedAt = _appliedAt }
        ]
    };
}
