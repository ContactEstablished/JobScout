using JobScout.Core.DTOs;
using JobScout.Web.Services;

namespace JobScout.Web.Tests.TestHelpers;

/// <summary>
/// Test double for <see cref="NotificationsService"/> that bypasses the real HttpClient.
/// Methods are non-virtual on the production type, so we subclass and use a custom HttpClient.
/// </summary>
public class StubNotificationsService : NotificationsService
{
    public int UnreadCount { get; set; }
    public List<NotificationDto> Items { get; set; } = [];
    public int GetCalls { get; private set; }
    public int MarkReadCalls { get; private set; }
    public Guid? LastMarkedReadId { get; private set; }

    public StubNotificationsService() : base(new HttpClient { BaseAddress = new Uri("http://stub/") })
    { }

    public override Task<IReadOnlyList<NotificationDto>> GetAsync(bool unreadOnly = false, int take = 50)
    {
        GetCalls++;
        return Task.FromResult<IReadOnlyList<NotificationDto>>(Items);
    }

    public override Task<int> GetUnreadCountAsync() => Task.FromResult(UnreadCount);

    public override Task<bool> MarkReadAsync(Guid id)
    {
        MarkReadCalls++;
        LastMarkedReadId = id;
        var match = Items.FirstOrDefault(n => n.Id == id);
        if (match is not null) match.IsRead = true;
        return Task.FromResult(true);
    }

    public override Task<int> MarkAllReadAsync()
    {
        foreach (var n in Items) n.IsRead = true;
        UnreadCount = 0;
        return Task.FromResult(Items.Count);
    }
}
