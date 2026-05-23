using JobScout.Core.Models;

namespace JobScout.Infrastructure.Tests.Builders;

public class AiScoreBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _jobId = Guid.NewGuid();
    private Guid _profileId = Guid.NewGuid();
    private decimal _score = 7m;
    private string _reasoning = "Solid fit.";
    private string _modelVersion = "claude-haiku-4-5-20251001";

    public AiScoreBuilder ForJob(Guid jobId) { _jobId = jobId; return this; }
    public AiScoreBuilder ForProfile(Guid profileId) { _profileId = profileId; return this; }
    public AiScoreBuilder WithScore(decimal score) { _score = score; return this; }
    public AiScoreBuilder WithModelVersion(string version) { _modelVersion = version; return this; }

    public AiScore Build() => new()
    {
        Id = _id,
        JobId = _jobId,
        ProfileId = _profileId,
        Score = _score,
        Reasoning = _reasoning,
        MatchedKeywords = "[]",
        GrowthAreas = "[]",
        RedFlags = "[]",
        ModelVersion = _modelVersion,
        ScoredAt = DateTime.UtcNow
    };
}
