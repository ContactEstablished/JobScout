using JobScout.Core.Models;

namespace JobScout.Core.Interfaces;

public interface IAiScoringService
{
    Task<AiScore> ScoreJobAsync(Job job, SearchProfile profile);
    Task<IReadOnlyList<AiScore>> BatchScoreAsync(IEnumerable<Job> jobs, SearchProfile profile);
    Task RecalibrateAsync(Guid profileId, bool resetHistory);
}
