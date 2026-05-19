using System.Net.Http.Json;
using JobScout.Core.DTOs;

namespace JobScout.Web.Services;

public class JobsService(HttpClient http)
{
    public async Task<PagedResult<JobSummaryDto>?> GetJobsAsync(
        Guid profileId, JobsFilter? filter = null, int page = 1, int pageSize = 20)
    {
        var qs = BuildJobsQuery(profileId, filter, page, pageSize);
        return await http.GetFromJsonAsync<PagedResult<JobSummaryDto>>($"api/jobs?{qs}");
    }

    public async Task<JobDetailDto?> GetJobAsync(Guid id, Guid? profileId = null)
    {
        var url = profileId.HasValue
            ? $"api/jobs/{id}?profileId={profileId}"
            : $"api/jobs/{id}";
        return await http.GetFromJsonAsync<JobDetailDto>(url);
    }

    private static string BuildJobsQuery(Guid profileId, JobsFilter? filter, int page, int pageSize)
    {
        var parts = new List<string>
        {
            $"profileId={profileId}",
            $"page={page}",
            $"pageSize={pageSize}"
        };

        if (filter?.Source is not null)
            parts.Add($"source={Uri.EscapeDataString(filter.Source)}");
        if (filter?.MinScore is not null)
            parts.Add($"minScore={filter.MinScore}");
        if (filter?.LocationType is not null)
            parts.Add($"locationType={Uri.EscapeDataString(filter.LocationType)}");
        if (filter?.JobType is not null)
            parts.Add($"jobType={Uri.EscapeDataString(filter.JobType)}");
        if (filter?.Query is not null)
            parts.Add($"q={Uri.EscapeDataString(filter.Query)}");

        return string.Join("&", parts);
    }
}
