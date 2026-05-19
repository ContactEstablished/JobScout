using JobScout.Core.Models;

namespace JobScout.Core.Interfaces;

public interface IJobBoardClient
{
    Task<IReadOnlyList<Job>> FetchJobsAsync(SearchProfile profile, CancellationToken ct = default);
}
