using JobScout.Api.Mapping;
using JobScout.Core.DTOs;
using JobScout.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobScout.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;
    private readonly ICurrentUserService _currentUser;

    public NotificationsController(
        INotificationService notifications,
        ICurrentUserService currentUser)
    {
        _notifications = notifications;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> Get(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int take = 50)
    {
        if (take is < 1 or > 200)
            return BadRequest("take must be between 1 and 200.");

        var items = await _notifications.GetForUserAsync(_currentUser.UserId, unreadOnly, take);
        return Ok(items.Select(n => n.ToDto()));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountDto>> GetUnreadCount()
    {
        var count = await _notifications.GetUnreadCountAsync(_currentUser.UserId);
        return Ok(new UnreadCountDto { Count = count });
    }

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var ok = await _notifications.MarkReadAsync(id, _currentUser.UserId);
        return ok ? NoContent() : NotFound();
    }

    [HttpPut("read-all")]
    public async Task<ActionResult<object>> MarkAllRead()
    {
        var count = await _notifications.MarkAllReadAsync(_currentUser.UserId);
        return Ok(new { updated = count });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ok = await _notifications.DeleteAsync(id, _currentUser.UserId);
        return ok ? NoContent() : NotFound();
    }
}
