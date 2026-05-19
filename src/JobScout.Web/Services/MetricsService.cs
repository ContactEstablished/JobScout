using System.Net.Http.Json;
using JobScout.Core.DTOs;

namespace JobScout.Web.Services;

public class MetricsService(HttpClient http)
{
    public async Task<DashboardStatsDto?> GetDashboardAsync(Guid profileId)
        => await http.GetFromJsonAsync<DashboardStatsDto>(
            $"api/metrics/dashboard?profileId={profileId}");

    public async Task<IReadOnlyList<SourceBreakdownDto>> GetSourceBreakdownAsync(Guid profileId)
        => await http.GetFromJsonAsync<List<SourceBreakdownDto>>(
            $"api/metrics/by-source?profileId={profileId}") ?? [];

    public async Task<IReadOnlyList<PostingWindowDto>> GetPostingWindowsAsync(Guid profileId)
        => await http.GetFromJsonAsync<List<PostingWindowDto>>(
            $"api/metrics/posting-windows?profileId={profileId}") ?? [];

    public async Task<IReadOnlyList<DashboardStatsDto>> GetTrendsAsync(Guid profileId, int days = 30)
        => await http.GetFromJsonAsync<List<DashboardStatsDto>>(
            $"api/metrics/trends?profileId={profileId}&days={days}") ?? [];
}
