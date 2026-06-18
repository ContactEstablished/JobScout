using System.Net.Http.Json;
using JobScout.Core.DTOs;

namespace JobScout.Web.Services;

public class FilterPresetsService(HttpClient http)
{
    public async Task<List<FilterPresetDto>> GetAsync(Guid profileId) =>
        await http.GetFromJsonAsync<List<FilterPresetDto>>($"api/filterpresets?profileId={profileId}") ?? [];

    public async Task<FilterPresetDto?> SaveAsync(SaveFilterPresetRequest request)
    {
        var response = await http.PostAsJsonAsync("api/filterpresets", request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<FilterPresetDto>()
            : null;
    }

    public async Task<bool> UpdateAsync(Guid id, SaveFilterPresetRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/filterpresets/{id}", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var response = await http.DeleteAsync($"api/filterpresets/{id}");
        return response.IsSuccessStatusCode;
    }
}
