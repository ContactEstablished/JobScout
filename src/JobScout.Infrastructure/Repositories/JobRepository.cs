using JobScout.Core.Enums;
using JobScout.Core.Interfaces;
using JobScout.Core.Models;
using JobScout.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace JobScout.Infrastructure.Repositories;

public class JobRepository : IJobRepository
{
    private readonly JobScoutDbContext _db;

    public JobRepository(JobScoutDbContext db) => _db = db;

    public async Task<Job?> GetByIdAsync(Guid id) =>
        await _db.Jobs
            .Include(j => j.AiScores)
            .Include(j => j.UserRatings)
            .FirstOrDefaultAsync(j => j.Id == id);

    public async Task<(IReadOnlyList<Job> Items, int TotalCount)> GetByProfileAsync(
        Guid profileId, JobFilterOptions? filters, int page, int pageSize)
    {
        var query = _db.Jobs
            .Include(j => j.AiScores.Where(s => s.ProfileId == profileId))
            .Include(j => j.UserRatings.Where(r => r.ProfileId == profileId))
            .Where(j => j.AiScores.Any(s => s.ProfileId == profileId))
            .Where(j => j.IsActive);

        if (filters is not null)
        {
            if (filters.Source.HasValue)
                query = query.Where(j => j.Source == filters.Source.Value);

            if (filters.LocationType.HasValue)
                query = query.Where(j => j.LocationType == filters.LocationType.Value);

            if (filters.JobType.HasValue)
                query = query.Where(j => j.JobType == filters.JobType.Value);

            if (!string.IsNullOrWhiteSpace(filters.Query))
            {
                var q = filters.Query.ToLower();
                query = query.Where(j =>
                    j.Title.ToLower().Contains(q) ||
                    j.Company.ToLower().Contains(q));
            }

            if (filters.MinScore.HasValue)
            {
                var minScore = filters.MinScore.Value;
                query = query.Where(j =>
                    j.AiScores.Any(s => s.ProfileId == profileId && s.Score >= minScore));
            }
        }

        query = query.OrderByDescending(j => j.DiscoveredAt);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Job?> GetByExternalIdAsync(string externalId, JobSource source) =>
        await _db.Jobs.FirstOrDefaultAsync(j => j.ExternalId == externalId && j.Source == source);

    public async Task AddAsync(Job job)
    {
        await _db.Jobs.AddAsync(job);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Job job)
    {
        _db.Jobs.Update(job);
        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Job>> GetRecentBySourceAsync(JobSource source, int days)
    {
        var cutoff = DateTime.UtcNow.AddDays(-days);
        return await _db.Jobs
            .Where(j => j.Source == source && j.DiscoveredAt >= cutoff)
            .OrderByDescending(j => j.DiscoveredAt)
            .ToListAsync();
    }
}
