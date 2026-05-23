using JobScout.Api.Tests.Fixtures;
using JobScout.Core.Interfaces;
using JobScout.Core.Models;
using JobScout.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobScout.Api.Tests.Controllers;

public class NotificationsControllerTests : IClassFixture<JobScoutWebApplicationFactory>
{
    private readonly JobScoutWebApplicationFactory _factory;

    public NotificationsControllerTests(JobScoutWebApplicationFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, string UserId)> CreateAuthedClientAsync(string suffix)
    {
        var client = _factory.CreateClient();
        var email = $"notif-{suffix}-{Guid.NewGuid():N}@example.com";
        var token = await _factory.RegisterAsync(client, email, "StrongPass1!", $"User {suffix}");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JobScoutDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        return (client, user.Id);
    }

    [Fact]
    public async Task GetUnreadCount_ReturnsZero_WhenNoNotifications()
    {
        var (client, _) = await CreateAuthedClientAsync("zero");
        var response = await client.GetAsync("/api/notifications/unread-count");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<UnreadCountDto>();
        dto!.Count.Should().Be(0);
    }

    [Fact]
    public async Task GetNotifications_ReturnsOnlyOwnedRows()
    {
        var (aClient, aUserId) = await CreateAuthedClientAsync("a");
        var (bClient, bUserId) = await CreateAuthedClientAsync("b");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JobScoutDbContext>();
            db.Notifications.AddRange(
                new Notification { Id = Guid.NewGuid(), UserId = aUserId, Type = NotificationType.NewStrongFit, Title = "A1", Message = "for A", CreatedAt = DateTime.UtcNow },
                new Notification { Id = Guid.NewGuid(), UserId = aUserId, Type = NotificationType.IngestionComplete, Title = "A2", Message = "also A", CreatedAt = DateTime.UtcNow },
                new Notification { Id = Guid.NewGuid(), UserId = bUserId, Type = NotificationType.NewStrongFit, Title = "B1", Message = "for B", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        var aList = await aClient.GetJsonAsync<List<NotificationDto>>("/api/notifications");
        aList!.Select(n => n.Title).Should().BeEquivalentTo(["A1", "A2"]);

        var bList = await bClient.GetJsonAsync<List<NotificationDto>>("/api/notifications");
        bList!.Select(n => n.Title).Should().BeEquivalentTo(["B1"]);
    }

    [Fact]
    public async Task MarkRead_ForOtherUsersNotification_Returns404()
    {
        var (aClient, _) = await CreateAuthedClientAsync("attacker");
        var (_, bUserId) = await CreateAuthedClientAsync("victim");

        Guid otherId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JobScoutDbContext>();
            var n = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = bUserId,
                Type = NotificationType.NewStrongFit,
                Title = "B private",
                Message = "secret",
                CreatedAt = DateTime.UtcNow
            };
            db.Notifications.Add(n);
            await db.SaveChangesAsync();
            otherId = n.Id;
        }

        var response = await aClient.PutAsync($"/api/notifications/{otherId}/read", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
