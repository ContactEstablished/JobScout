using System.Net.Http.Json;
using JobScout.Core.DTOs;
using Microsoft.AspNetCore.Components.Forms;

namespace JobScout.Web.Services;

public class ProfilesService(HttpClient http)
{
    public async Task<IReadOnlyList<SearchProfileDto>> GetAllAsync()
        => await http.GetFromJsonAsync<List<SearchProfileDto>>("api/profiles") ?? [];

    public async Task<SearchProfileDto?> GetByIdAsync(Guid id)
        => await http.GetFromJsonAsync<SearchProfileDto>($"api/profiles/{id}");

    public async Task<SearchProfileDto?> CreateAsync(CreateProfileRequest request)
    {
        var response = await http.PostAsJsonAsync("api/profiles", request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<SearchProfileDto>()
            : null;
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateProfileRequest request)
    {
        var response = await http.PutAsJsonAsync($"api/profiles/{id}", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var response = await http.DeleteAsync($"api/profiles/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UploadResumeAsync(Guid id, IBrowserFile file)
    {
        using var content = new MultipartFormDataContent();
        var stream = file.OpenReadStream(maxAllowedSize: 10_000_000);
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
        content.Add(fileContent, "file", file.Name);
        var response = await http.PostAsync($"api/profiles/{id}/resume", content);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RecalibrateAsync(Guid id, RecalibrateRequest request)
    {
        var response = await http.PostAsJsonAsync($"api/profiles/{id}/recalibrate", request);
        return response.IsSuccessStatusCode;
    }
}
