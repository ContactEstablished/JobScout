using JobScout.Core.DTOs;
using JobScout.Core.Interfaces;
using JobScout.Core.Models;
using JobScout.Infrastructure.Configuration;
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
    private static readonly string[] IntegrationKeys =
    [
        "Anthropic:ApiKey",
        "SerpApi:ApiKey",
        "Adzuna:AppId",
        "Adzuna:AppKey",
        "SendGrid:ApiKey",
        "SendGrid:FromAddress",
        "Wellfound:AccessToken",
    ];

    private readonly JobScoutDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISecretStore _secrets;

    public SettingsController(
        JobScoutDbContext db,
        ICurrentUserService currentUser,
        ISecretStore secrets)
    {
        _db = db;
        _currentUser = currentUser;
        _secrets = secrets;
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

    [HttpGet("integrations")]
    public async Task<ActionResult<IntegrationSettingsDto>> GetIntegrations()
    {
        // Return masked values so secrets never leave the box; the FromAddress is not a secret.
        var dto = new IntegrationSettingsDto
        {
            AnthropicApiKey = Mask(await _secrets.GetAsync("Anthropic:ApiKey")),
            SerpApiKey = Mask(await _secrets.GetAsync("SerpApi:ApiKey")),
            AdzunaAppId = Mask(await _secrets.GetAsync("Adzuna:AppId")),
            AdzunaAppKey = Mask(await _secrets.GetAsync("Adzuna:AppKey")),
            SendGridApiKey = Mask(await _secrets.GetAsync("SendGrid:ApiKey")),
            SendGridFromAddress = await _secrets.GetAsync("SendGrid:FromAddress"),
            WellfoundAccessToken = Mask(await _secrets.GetAsync("Wellfound:AccessToken"))
        };
        return Ok(dto);
    }

    [HttpPut("integrations")]
    public async Task<IActionResult> UpdateIntegrations([FromBody] IntegrationSettingsDto request)
    {
        // Any field left as null is treated as "no change"; empty string clears the value.
        if (request.AnthropicApiKey is not null) await _secrets.SetAsync("Anthropic:ApiKey", request.AnthropicApiKey);
        if (request.SerpApiKey is not null) await _secrets.SetAsync("SerpApi:ApiKey", request.SerpApiKey);
        if (request.AdzunaAppId is not null) await _secrets.SetAsync("Adzuna:AppId", request.AdzunaAppId);
        if (request.AdzunaAppKey is not null) await _secrets.SetAsync("Adzuna:AppKey", request.AdzunaAppKey);
        if (request.SendGridApiKey is not null) await _secrets.SetAsync("SendGrid:ApiKey", request.SendGridApiKey);
        if (request.SendGridFromAddress is not null) await _secrets.SetAsync("SendGrid:FromAddress", request.SendGridFromAddress);
        if (request.WellfoundAccessToken is not null) await _secrets.SetAsync("Wellfound:AccessToken", request.WellfoundAccessToken);
        return NoContent();
    }

    private static string? Mask(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (value.Length <= 4) return new string('•', value.Length);
        return new string('•', Math.Max(4, value.Length - 4)) + value[^4..];
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
