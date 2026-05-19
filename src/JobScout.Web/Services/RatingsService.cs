using System.Net.Http.Json;
using JobScout.Core.DTOs;

namespace JobScout.Web.Services;

public class RatingsService(HttpClient http)
{
    public async Task<UserRatingDto?> UpsertAsync(UserRatingRequest request)
    {
        var response = await http.PostAsJsonAsync("api/ratings", request);
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<UserRatingDto>();
    }
}
