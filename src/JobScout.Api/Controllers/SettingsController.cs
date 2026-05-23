using JobScout.Core.DTOs;
using JobScout.Core.Interfaces;
using JobScout.Core.Models;
using JobScout.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JobScout.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly JobScoutDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public SettingsController(JobScoutDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet("notifications")]
    public async Task<ActionResult<NotificationPreferencesDto>> GetNotifications()
    {
        var prefs = await GetOrCreateAsync(_currentUser.UserId);
        return Ok(ToDto(prefs));
    }

    [HttpPut("notifications")]
    public async Task<ActionResult<NotificationPreferencesDto>> UpdateNotifications(
        [FromBody] NotificationPreferencesDto request)
    {
        if (!IsValidTimeZone(request.TimeZoneId))
            return BadRequest($"Unknown time zone id '{request.TimeZoneId}'.");

        var prefs = await GetOrCreateAsync(_currentUser.UserId);
        prefs.InAppNewStrongFit = request.InAppNewStrongFit;
        prefs.InAppScoreUpdate = request.InAppScoreUpdate;
        prefs.InAppIngestionComplete = request.InAppIngestionComplete;
        prefs.InAppApplicationStatusChange = request.InAppApplicationStatusChange;
        prefs.EmailDailyDigest = request.EmailDailyDigest;
        prefs.EmailWeeklySummary = request.EmailWeeklySummary;
        prefs.EmailInstantStrongMatch = request.EmailInstantStrongMatch;
        prefs.QuietHoursStart = request.QuietHoursStart;
        prefs.QuietHoursEnd = request.QuietHoursEnd;
        prefs.TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId) ? "UTC" : request.TimeZoneId;

        await _db.SaveChangesAsync();
        return Ok(ToDto(prefs));
    }

    private async Task<NotificationPreferences> GetOrCreateAsync(string userId)
    {
        var prefs = await _db.NotificationPreferences.FirstOrDefaultAsync(p => p.UserId == userId);
        if (prefs is null)
        {
            prefs = new NotificationPreferences { UserId = userId };
            _db.NotificationPreferences.Add(prefs);
            await _db.SaveChangesAsync();
        }
        return prefs;
    }

    private static bool IsValidTimeZone(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return true;
        try { TimeZoneInfo.FindSystemTimeZoneById(id); return true; }
        catch { return false; }
    }

    private static NotificationPreferencesDto ToDto(NotificationPreferences p) => new()
    {
        InAppNewStrongFit = p.InAppNewStrongFit,
        InAppScoreUpdate = p.InAppScoreUpdate,
        InAppIngestionComplete = p.InAppIngestionComplete,
        InAppApplicationStatusChange = p.InAppApplicationStatusChange,
        EmailDailyDigest = p.EmailDailyDigest,
        EmailWeeklySummary = p.EmailWeeklySummary,
        EmailInstantStrongMatch = p.EmailInstantStrongMatch,
        QuietHoursStart = p.QuietHoursStart,
        QuietHoursEnd = p.QuietHoursEnd,
        TimeZoneId = p.TimeZoneId
    };
}
