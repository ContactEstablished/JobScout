using Bunit;
using JobScout.Core.DTOs;
using JobScout.Core.Enums;
using JobScout.Web.Components;
using JobScout.Web.Services;
using JobScout.Web.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;

namespace JobScout.Web.Tests.Components;

public class NotificationDropdownTests : TestContext
{
    private StubNotificationsService RegisterStub()
    {
        var stub = new StubNotificationsService();
        Services.AddSingleton<NotificationsService>(stub);
        return stub;
    }

    [Fact]
    public void RendersBellWithoutBadge_WhenUnreadCountIsZero()
    {
        RegisterStub().UnreadCount = 0;

        var cut = RenderComponent<NotificationDropdown>();

        cut.Markup.Should().NotContain("notif-badge");
        cut.Find("button.notif-bell").Should().NotBeNull();
    }

    [Fact]
    public void RendersBadgeWithCount_WhenLessThanTen()
    {
        RegisterStub().UnreadCount = 3;

        var cut = RenderComponent<NotificationDropdown>();
        // Allow async refresh to settle
        cut.WaitForAssertion(() => cut.Find(".notif-badge").TextContent.Should().Be("3"));
    }

    [Fact]
    public void RendersNinePlusBadge_WhenCountAtLeastTen()
    {
        RegisterStub().UnreadCount = 12;

        var cut = RenderComponent<NotificationDropdown>();
        cut.WaitForAssertion(() => cut.Find(".notif-badge").TextContent.Should().Be("9+"));
    }

    [Fact]
    public void ClickingBell_OpensPanelAndFetchesNotifications()
    {
        var stub = RegisterStub();
        stub.Items = [
            new NotificationDto { Id = Guid.NewGuid(), Type = NotificationType.NewStrongFit, Title = "Match!", Message = "Score 9.0", CreatedAt = DateTime.UtcNow }
        ];

        var cut = RenderComponent<NotificationDropdown>();
        cut.Find("button.notif-bell").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Find(".notif-panel").Should().NotBeNull();
            stub.GetCalls.Should().BeGreaterThan(0);
            cut.Markup.Should().Contain("Match!");
        });
    }

    [Fact]
    public void ClickingNotification_MarksItRead()
    {
        var stub = RegisterStub();
        var id = Guid.NewGuid();
        stub.UnreadCount = 1;
        stub.Items = [
            new NotificationDto { Id = id, Type = NotificationType.IngestionComplete, Title = "5 new jobs", Message = "From RemoteOK", CreatedAt = DateTime.UtcNow, IsRead = false }
        ];

        var cut = RenderComponent<NotificationDropdown>();
        cut.Find("button.notif-bell").Click();
        cut.WaitForAssertion(() => cut.Find(".notif-item"));

        cut.Find(".notif-item").Click();

        cut.WaitForAssertion(() =>
        {
            stub.MarkReadCalls.Should().Be(1);
            stub.LastMarkedReadId.Should().Be(id);
        });
    }
}
