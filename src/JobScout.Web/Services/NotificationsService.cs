using System.Net.Http.Json;
using JobScout.Core.DTOs;

namespace JobScout.Web.Services;

public class NotificationsService(HttpClient http)
{
    public async Task<IReadOnlyList<NotificationDto>> GetAsync(bool unreadOnly = false, int take = 50)
    {
        var url = $"api/notifications?unreadOnly={unreadOnly.ToString().ToLowerInvariant()}&take={take}";
        var items = await http.GetFromJsonAsync<List<NotificationDto>>(url);
        return items ?? [];
    }

    public async Task<int> GetUnreadCountAsync()
    {
        var dto = await http.GetFromJsonAsync<UnreadCountDto>("api/notifications/unread-count");
        return dto?.Count ?? 0;
    }

    public async Task<bool> MarkReadAsync(Guid id)
    {
        var response = await http.PutAsync($"api/notifications/{id}/read", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<int> MarkAllReadAsync()
    {
        var response = await http.PutAsync("api/notifications/read-all", null);
        if (!response.IsSuccessStatusCode) return 0;
        var body = await response.Content.ReadFromJsonAsync<MarkAllResult>();
        return body?.Updated ?? 0;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var response = await http.DeleteAsync($"api/notifications/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<NotificationPreferencesDto?> GetPreferencesAsync()
        => await http.GetFromJsonAsync<NotificationPreferencesDto>("api/settings/notifications");

    public async Task<NotificationPreferencesDto?> UpdatePreferencesAsync(NotificationPreferencesDto prefs)
    {
        var response = await http.PutAsJsonAsync("api/settings/notifications", prefs);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<NotificationPreferencesDto>();
    }

    private sealed record MarkAllResult(int Updated);
}
