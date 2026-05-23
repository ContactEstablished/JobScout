using JobScout.Core.DTOs;
using JobScout.Core.Enums;
using JobScout.Core.Interfaces;
using JobScout.Core.Models;
using JobScout.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace JobScout.Infrastructure.Repositories;

public class ApplicationRepository : IApplicationRepository
{
    private readonly JobScoutDbContext _db;

    public ApplicationRepository(JobScoutDbContext db) => _db = db;

    public async Task<IReadOnlyList<JobApplication>> GetByProfileAsync(
        Guid profileId, string userId, ApplicationStatus? status = null)
    {
        var query = _db.JobApplications
            .Include(a => a.Job)
            .Include(a => a.Profile)
            .Where(a => a.ProfileId == profileId && a.Profile.UserId == userId);

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);

        return await query.OrderByDescending(a => a.AppliedAt).ToListAsync();
    }

    public async Task<JobApplication?> GetByIdAsync(Guid id, string userId)
    {
        return await _db.JobApplications
            .Include(a => a.Job)
            .Include(a => a.Profile)
            .FirstOrDefaultAsync(a => a.Id == id && a.Profile.UserId == userId);
    }

    public async Task<JobApplication?> GetByJobAsync(Guid jobId, Guid profileId, string userId)
    {
        return await _db.JobApplications
            .Include(a => a.Job)
            .Include(a => a.Profile)
            .FirstOrDefaultAsync(a => a.JobId == jobId && a.ProfileId == profileId && a.Profile.UserId == userId);
    }

    public async Task<PipelineDto> GetPipelineAsync(Guid profileId, string userId)
    {
        var apps = await _db.JobApplications
            .Where(a => a.ProfileId == profileId && a.Profile.UserId == userId)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        return new PipelineDto
        {
            Applied = apps.FirstOrDefault(a => a.Status == ApplicationStatus.Applied)?.Count ?? 0,
            Interviewing = apps.FirstOrDefault(a => a.Status == ApplicationStatus.Interviewing)?.Count ?? 0,
            Offered = apps.FirstOrDefault(a => a.Status == ApplicationStatus.Offered)?.Count ?? 0,
            Accepted = apps.FirstOrDefault(a => a.Status == ApplicationStatus.Accepted)?.Count ?? 0,
            Rejected = apps.FirstOrDefault(a => a.Status == ApplicationStatus.Rejected)?.Count ?? 0,
            Withdrawn = apps.FirstOrDefault(a => a.Status == ApplicationStatus.Withdrawn)?.Count ?? 0
        };
    }

    public async Task AddAsync(JobApplication application)
    {
        _db.JobApplications.Add(application);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(JobApplication application)
    {
        _db.JobApplications.Update(application);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id, string userId)
    {
        var app = await _db.JobApplications
            .Include(a => a.Profile)
            .FirstOrDefaultAsync(a => a.Id == id && a.Profile.UserId == userId);

        if (app is not null)
        {
            _db.JobApplications.Remove(app);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<int> GetActiveCountAsync(string userId)
    {
        return await _db.JobApplications
            .Where(a => a.Profile.UserId == userId
                && a.Status != ApplicationStatus.Rejected
                && a.Status != ApplicationStatus.Withdrawn)
            .CountAsync();
    }
}
