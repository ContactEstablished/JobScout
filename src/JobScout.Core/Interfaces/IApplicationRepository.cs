using JobScout.Core.DTOs;
using JobScout.Core.Enums;
using JobScout.Core.Models;

namespace JobScout.Core.Interfaces;

public interface IApplicationRepository
{
    Task<IReadOnlyList<JobApplication>> GetByProfileAsync(Guid profileId, string userId, ApplicationStatus? status = null);
    Task<JobApplication?> GetByIdAsync(Guid id, string userId);
    Task<JobApplication?> GetByJobAsync(Guid jobId, Guid profileId, string userId);
    Task<PipelineDto> GetPipelineAsync(Guid profileId, string userId);
    Task AddAsync(JobApplication application);
    Task UpdateAsync(JobApplication application);
    Task DeleteAsync(Guid id, string userId);
    Task<int> GetActiveCountAsync(string userId);
}
