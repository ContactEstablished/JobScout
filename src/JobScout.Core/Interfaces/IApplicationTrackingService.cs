using JobScout.Core.DTOs;
using JobScout.Core.Enums;
using JobScout.Core.Models;

namespace JobScout.Core.Interfaces;

public interface IApplicationTrackingService
{
    Task<JobApplication> ApplyAsync(Guid jobId, Guid profileId, string userId, string? notes = null);
    Task<JobApplication> UpdateStatusAsync(Guid applicationId, ApplicationStatus newStatus, string userId, string? notes = null);
    Task<PipelineDto> GetPipelineAsync(Guid profileId, string userId);
}
