using JobScout.Core.Enums;
using JobScout.Core.Interfaces;
using JobScout.Core.Models;

namespace JobScout.Infrastructure.Tests.TestHelpers;

public class StubJobBoardClient : IJobBoardClient
{
    private readonly IReadOnlyList<Job> _jobs;
    private readonly Exception? _throwOnFetch;
    public int CallCount { get; private set; }

    public StubJobBoardClient(JobSource source, IReadOnlyList<Job> jobs, Exception? throwOnFetch = null)
    {
        Source = source;
        _jobs = jobs;
        _throwOnFetch = throwOnFetch;
    }

    public JobSource Source { get; }

    public Task<IReadOnlyList<Job>> FetchJobsAsync(SearchProfile profile, CancellationToken ct = default)
    {
        CallCount++;
        if (_throwOnFetch is not null) throw _throwOnFetch;
        return Task.FromResult(_jobs);
    }
}
