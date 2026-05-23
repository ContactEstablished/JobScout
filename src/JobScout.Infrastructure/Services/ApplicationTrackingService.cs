using JobScout.Core.DTOs;
using JobScout.Core.Enums;
using JobScout.Core.Interfaces;
using JobScout.Core.Models;
using JobScout.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobScout.Infrastructure.Services;

public class ApplicationTrackingService(
    IApplicationRepository applications,
    IJobRepository jobs,
    IProfileRepository profiles,
    JobScoutDbContext db,
    INotificationService notifications,
    ILogger<ApplicationTrackingService> logger) : IApplicationTrackingService
{
    // Valid status transitions
    private static readonly Dictionary<ApplicationStatus, ApplicationStatus[]> ValidTransitions = new()
    {
        [ApplicationStatus.Applied] = [ApplicationStatus.Interviewing, ApplicationStatus.Rejected, ApplicationStatus.Withdrawn],
        [ApplicationStatus.Interviewing] = [ApplicationStatus.Offered, ApplicationStatus.Rejected, ApplicationStatus.Withdrawn],
        [ApplicationStatus.Offered] = [ApplicationStatus.Accepted, ApplicationStatus.Rejected, ApplicationStatus.Withdrawn],
        [ApplicationStatus.Accepted] = [ApplicationStatus.Withdrawn],
        [ApplicationStatus.Rejected] = [],
        [ApplicationStatus.Withdrawn] = []
    };

    public async Task<JobApplication> ApplyAsync(Guid jobId, Guid profileId, string userId, string? notes = null)
    {
        // Verify job exists
        var job = await jobs.GetByIdAsync(jobId)
            ?? throw new InvalidOperationException("Job not found.");

        // Verify profile belongs to user
        var profile = await profiles.GetByIdAsync(profileId, userId)
            ?? throw new InvalidOperationException("Profile not found.");

        // Check for existing application
        var existing = await applications.GetByJobAsync(jobId, profileId, userId);
        if (existing is not null)
            throw new InvalidOperationException("Application already exists for this job and profile.");

        var now = DateTime.UtcNow;
        var application = new JobApplication
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            ProfileId = profileId,
            AppliedAt = now,
            Status = ApplicationStatus.Applied,
            Notes = notes,
            StatusHistory =
            [
                new StatusChange
                {
                    Status = ApplicationStatus.Applied,
                    ChangedAt = now,
                    Notes = notes
                }
            ]
        };

        await applications.AddAsync(application);

        // Auto-increment DailyMetric.Applied
        await IncrementAppliedMetricAsync(profileId, job.Source);

        logger.LogInformation("Application created: {AppId} for job {JobId} profile {ProfileId}",
            application.Id, jobId, profileId);

        // Re-fetch to include navigation properties
        return (await applications.GetByIdAsync(application.Id, userId))!;
    }

    public async Task<JobApplication> UpdateStatusAsync(
        Guid applicationId, ApplicationStatus newStatus, string userId, string? notes = null)
    {
        var app = await applications.GetByIdAsync(applicationId, userId)
            ?? throw new InvalidOperationException("Application not found.");

        // Validate transition
        if (!ValidTransitions.TryGetValue(app.Status, out var allowed) || !allowed.Contains(newStatus))
            throw new InvalidOperationException(
                $"Cannot transition from {app.Status} to {newStatus}.");

        var oldStatus = app.Status;
        app.Status = newStatus;
        app.StatusHistory.Add(new StatusChange
        {
            Status = newStatus,
            ChangedAt = DateTime.UtcNow,
            Notes = notes
        });

        await applications.UpdateAsync(app);
        logger.LogInformation("Application {AppId} status changed: {OldStatus} → {NewStatus}",
            applicationId, oldStatus, newStatus);

        try
        {
            await notifications.OnApplicationStatusChangedAsync(app, oldStatus, newStatus, userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to emit application-status notification for {AppId}", app.Id);
        }

        return app;
    }

    public async Task<PipelineDto> GetPipelineAsync(Guid profileId, string userId)
    {
        return await applications.GetPipelineAsync(profileId, userId);
    }

    private async Task IncrementAppliedMetricAsync(Guid profileId, JobSource source)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var metric = await db.DailyMetrics
            .FirstOrDefaultAsync(m => m.ProfileId == profileId && m.Date == today && m.Source == source);

        if (metric is not null)
        {
            metric.Applied++;
        }
        else
        {
            db.DailyMetrics.Add(new DailyMetric
            {
                Id = Guid.NewGuid(),
                ProfileId = profileId,
                Date = today,
                Source = source,
                Applied = 1
            });
        }

        await db.SaveChangesAsync();
    }
}
