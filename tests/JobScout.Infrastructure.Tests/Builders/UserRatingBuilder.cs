using JobScout.Core.Models;

namespace JobScout.Infrastructure.Tests.Builders;

public class UserRatingBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _jobId = Guid.NewGuid();
    private Guid _profileId = Guid.NewGuid();
    private int _stars = 4;
    private string? _notes;
    private DateTime _ratedAt = DateTime.UtcNow;

    public UserRatingBuilder ForJob(Guid jobId) { _jobId = jobId; return this; }
    public UserRatingBuilder ForProfile(Guid profileId) { _profileId = profileId; return this; }
    public UserRatingBuilder WithStars(int stars) { _stars = stars; return this; }
    public UserRatingBuilder WithNotes(string notes) { _notes = notes; return this; }
    public UserRatingBuilder RatedAt(DateTime when) { _ratedAt = when; return this; }

    public UserRating Build() => new()
    {
        Id = _id,
        JobId = _jobId,
        ProfileId = _profileId,
        Stars = _stars,
        Notes = _notes,
        RatedAt = _ratedAt
    };
}
