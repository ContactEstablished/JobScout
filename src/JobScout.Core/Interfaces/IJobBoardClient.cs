using JobScout.Core.Enums;
using JobScout.Core.Models;

namespace JobScout.Core.Interfaces;

public interface IJobBoardClient
{
    JobSource Source { get; }
    Task<IReadOnlyList<Job>> FetchJobsAsync(SearchProfile profile, CancellationToken ct = default);
}
