using System.Net.Http.Json;
using JobScout.Core.DTOs;

namespace JobScout.Web.Services;

public class SetupService(HttpClient http)
{
    public virtual async Task<bool> NeedsSetupAsync()
    {
        try
        {
            var dto = await http.GetFromJsonAsync<SetupStatusDto>("api/setup/status");
            return dto?.NeedsSetup ?? false;
        }
        catch
        {
            // If the API isn't reachable yet, don't redirect to setup.
            return false;
        }
    }

    public virtual async Task<AuthResponse?> CompleteAsync(CompleteSetupRequest request)
    {
        var response = await http.PostAsJsonAsync("api/setup/complete", request);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<AuthResponse>();
    }
}
