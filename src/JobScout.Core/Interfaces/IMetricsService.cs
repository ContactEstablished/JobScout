using JobScout.Core.DTOs;

namespace JobScout.Core.Interfaces;

public interface IMetricsService
{
    Task<DashboardStatsDto> GetDashboardStatsAsync(Guid profileId);
    Task<IReadOnlyList<SourceBreakdownDto>> GetSourceBreakdownAsync(Guid profileId);
    Task<IReadOnlyList<PostingWindowDto>> GetPostingWindowsAsync(Guid profileId);
    Task<IReadOnlyList<DashboardStatsDto>> GetTrendsAsync(Guid profileId, int days);
}
