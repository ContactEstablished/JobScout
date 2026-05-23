using JobScout.Core.DTOs;
using JobScout.Core.Models;

namespace JobScout.Api.Mapping;

public static class MappingExtensions
{
    public static JobSummaryDto ToSummaryDto(this Job job, Guid? profileId = null)
    {
        var score = profileId.HasValue
            ? job.AiScores.FirstOrDefault(s => s.ProfileId == profileId.Value)?.ToDto()
            : job.AiScores.MaxBy(s => s.Score)?.ToDto();

        var rating = profileId.HasValue
            ? job.UserRatings.FirstOrDefault(r => r.ProfileId == profileId.Value)?.ToDto()
            : null;

        return new JobSummaryDto
        {
            Id = job.Id,
            Title = job.Title,
            Company = job.Company,
            Location = job.Location,
            LocationType = job.LocationType,
            JobType = job.JobType,
            Tags = job.Tags,
            Salary = job.Salary,
            PostedAt = job.PostedAt,
            DiscoveredAt = job.DiscoveredAt,
            Source = job.Source,
            SourceUrl = job.SourceUrl,
            AiScore = score,
            UserRating = rating
        };
    }

    public static JobDetailDto ToDetailDto(this Job job, Guid? profileId = null)
    {
        var score = profileId.HasValue
            ? job.AiScores.FirstOrDefault(s => s.ProfileId == profileId.Value)?.ToDto()
            : job.AiScores.MaxBy(s => s.Score)?.ToDto();

        var rating = profileId.HasValue
            ? job.UserRatings.FirstOrDefault(r => r.ProfileId == profileId.Value)?.ToDto()
            : null;

        return new JobDetailDto
        {
            Id = job.Id,
            ExternalId = job.ExternalId,
            Title = job.Title,
            Company = job.Company,
            Location = job.Location,
            LocationType = job.LocationType,
            JobType = job.JobType,
            Description = job.Description,
            Tags = job.Tags,
            Salary = job.Salary,
            PostedAt = job.PostedAt,
            DiscoveredAt = job.DiscoveredAt,
            Source = job.Source,
            SourceUrl = job.SourceUrl,
            AiScore = score,
            UserRating = rating
        };
    }

    public static AiScoreDto ToDto(this AiScore score) => new()
    {
        Id = score.Id,
        JobId = score.JobId,
        ProfileId = score.ProfileId,
        Score = score.Score,
        Reasoning = score.Reasoning,
        MatchedKeywords = score.MatchedKeywords,
        ScoredAt = score.ScoredAt,
        ModelVersion = score.ModelVersion
    };

    public static UserRatingDto ToDto(this UserRating rating) => new()
    {
        Id = rating.Id,
        JobId = rating.JobId,
        ProfileId = rating.ProfileId,
        Stars = rating.Stars,
        Notes = rating.Notes,
        RatedAt = rating.RatedAt
    };

    public static SearchProfileDto ToDto(this SearchProfile profile) => new()
    {
        Id = profile.Id,
        Name = profile.Name,
        Description = profile.Description,
        ResumeFileName = profile.ResumeFileName,
        LinkedInUrl = profile.LinkedInUrl,
        CreatedAt = profile.CreatedAt,
        UpdatedAt = profile.UpdatedAt,
        IsActive = profile.IsActive,
        SearchKeywords = profile.SearchKeywords,
        PreferredSources = profile.PreferredSources,
        PreferredJobTypes = profile.PreferredJobTypes,
        PreferredLocationTypes = profile.PreferredLocationTypes,
        LocationPreference = profile.LocationPreference,
        DetectedSkills = profile.DetectedSkills,
        ProfileColor = profile.ProfileColor
    };

    public static ApplicationDto ToDto(this JobApplication app) => new()
    {
        Id = app.Id,
        JobId = app.JobId,
        ProfileId = app.ProfileId,
        AppliedAt = app.AppliedAt,
        Status = app.Status,
        Notes = app.Notes,
        StatusHistory = app.StatusHistory.Select(s => new StatusChangeDto
        {
            Status = s.Status,
            ChangedAt = s.ChangedAt,
            Notes = s.Notes
        }).ToList(),
        Job = app.Job?.ToSummaryDto(app.ProfileId)
    };
}
