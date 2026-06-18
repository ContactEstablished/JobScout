using JobScout.Core.Enums;
using JobScout.Core.Models;

namespace JobScout.Core.Interfaces;

public class JobFilterOptions
{
    public JobSource? Source { get; set; }
    public decimal? MinScore { get; set; }
    public LocationType? LocationType { get; set; }
    public JobType? JobType { get; set; }
    public string? Query { get; set; }
    public JobSortBy SortBy { get; set; } = JobSortBy.AiScore;
}

public interface IJobRepository
{
    Task<Job?> GetByIdAsync(Guid id);
    Task<(IReadOnlyList<Job> Items, int TotalCount)> GetByProfileAsync(Guid profileId, JobFilterOptions? filters, int page, int pageSize);
    Task<Job?> GetByExternalIdAsync(string externalId, JobSource source);
    Task AddAsync(Job job);
    Task UpdateAsync(Job job);
    Task<IReadOnlyList<Job>> GetRecentBySourceAsync(JobSource source, int days);
    Task<Job?> FindFuzzyDuplicateAsync(string normalizedTitle, string normalizedCompany, JobSource excludeSource);
}
