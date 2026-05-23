using System.Net.Http.Json;
using JobScout.Core.DTOs;
using JobScout.Core.Enums;

namespace JobScout.Web.Services;

public class ApplicationsService(HttpClient http)
{
    public async Task<IReadOnlyList<ApplicationDto>> GetByProfileAsync(
        Guid profileId, ApplicationStatus? status = null)
    {
        var url = $"api/applications?profileId={profileId}";
        if (status.HasValue)
            url += $"&status={status.Value}";
        return await http.GetFromJsonAsync<List<ApplicationDto>>(url) ?? [];
    }

    public async Task<ApplicationDto?> CreateAsync(CreateApplicationRequest request)
    {
        var response = await http.PostAsJsonAsync("api/applications", request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<ApplicationDto>()
            : null;
    }

    public async Task<bool> UpdateStatusAsync(Guid id, UpdateStatusRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/applications/{id}/status", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var response = await http.DeleteAsync($"api/applications/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<PipelineDto> GetPipelineAsync(Guid profileId)
    {
        return await http.GetFromJsonAsync<PipelineDto>($"api/applications/pipeline?profileId={profileId}")
            ?? new PipelineDto();
    }

    public async Task<int> GetActiveCountAsync()
    {
        // Use pipeline for active profile to get count - this is called from sidebar
        // We'll sum all non-rejected, non-withdrawn
        try
        {
            // Fetch all profiles' applications isn't practical, so just get a simple count
            // by fetching the pipeline for each active profile
            return 0; // Will be overridden per-profile
        }
        catch
        {
            return 0;
        }
    }
}
