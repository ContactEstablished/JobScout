using JobScout.Core.DTOs;
using JobScout.Core.Enums;
using JobScout.Core.Interfaces;
using JobScout.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace JobScout.Infrastructure.Services;

public class MetricsService : IMetricsService
{
    private readonly JobScoutDbContext _db;

    public MetricsService(JobScoutDbContext db) => _db = db;

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(Guid profileId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var prevWeekStart = weekStart.AddDays(-7);

        var thisWeek = await _db.DailyMetrics
            .Where(m => m.ProfileId == profileId && m.Date >= weekStart)
            .ToListAsync();

        var prevWeek = await _db.DailyMetrics
            .Where(m => m.ProfileId == profileId && m.Date >= prevWeekStart && m.Date < weekStart)
            .ToListAsync();

        var savedThisWeek = await _db.UserRatings
            .CountAsync(r => r.ProfileId == profileId && r.Stars >= 4
                && r.RatedAt >= weekStart.ToDateTime(TimeOnly.MinValue));

        var savedPrevWeek = await _db.UserRatings
            .CountAsync(r => r.ProfileId == profileId && r.Stars >= 4
                && r.RatedAt >= prevWeekStart.ToDateTime(TimeOnly.MinValue)
                && r.RatedAt < weekStart.ToDateTime(TimeOnly.MinValue));

        return new DashboardStatsDto
        {
            JobsFound = thisWeek.Sum(m => m.JobsFound),
            StrongFits = thisWeek.Sum(m => m.StrongFits),
            Saved = savedThisWeek,
            Applied = thisWeek.Sum(m => m.Applied),
            JobsFoundDelta = thisWeek.Sum(m => m.JobsFound) - prevWeek.Sum(m => m.JobsFound),
            StrongFitsDelta = thisWeek.Sum(m => m.StrongFits) - prevWeek.Sum(m => m.StrongFits),
            SavedDelta = savedThisWeek - savedPrevWeek,
            AppliedDelta = thisWeek.Sum(m => m.Applied) - prevWeek.Sum(m => m.Applied)
        };
    }

    public async Task<IReadOnlyList<SourceBreakdownDto>> GetSourceBreakdownAsync(Guid profileId)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));

        var rows = await _db.DailyMetrics
            .Where(m => m.ProfileId == profileId && m.Date >= cutoff)
            .GroupBy(m => m.Source)
            .Select(g => new SourceBreakdownDto
            {
                Source = g.Key,
                SourceName = g.Key.ToString(),
                JobCount = g.Sum(m => m.JobsFound),
                StrongFitCount = g.Sum(m => m.StrongFits)
            })
            .OrderByDescending(s => s.JobCount)
            .ToListAsync();

        return rows;
    }

    public async Task<IReadOnlyList<PostingWindowDto>> GetPostingWindowsAsync(Guid profileId)
    {
        var cutoff = DateTime.UtcNow.AddDays(-30);

        var jobs = await _db.Jobs
            .Where(j => j.AiScores.Any(s => s.ProfileId == profileId)
                && j.PostedAt.HasValue
                && j.PostedAt >= cutoff)
            .Select(j => j.PostedAt!.Value.DayOfWeek)
            .ToListAsync();

        var allDays = Enum.GetValues<DayOfWeek>();
        var grouped = jobs.GroupBy(d => d).ToDictionary(g => g.Key, g => g.Count());

        return allDays.Select(day => new PostingWindowDto
        {
            DayOfWeek = day,
            DayName = day.ToString(),
            JobCount = grouped.GetValueOrDefault(day, 0),
            BestTimeWindow = "9am–12pm"
        }).ToList();
    }

    public async Task<IReadOnlyList<DashboardStatsDto>> GetTrendsAsync(Guid profileId, int days)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days));

        var rows = await _db.DailyMetrics
            .Where(m => m.ProfileId == profileId && m.Date >= cutoff)
            .GroupBy(m => m.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DashboardStatsDto
            {
                Date = g.Key,
                JobsFound = g.Sum(m => m.JobsFound),
                StrongFits = g.Sum(m => m.StrongFits),
                Applied = g.Sum(m => m.Applied)
            })
            .ToListAsync();

        return rows;
    }
}
