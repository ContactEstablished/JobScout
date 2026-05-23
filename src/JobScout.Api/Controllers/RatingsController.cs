using JobScout.Api.Mapping;
using JobScout.Core.DTOs;
using JobScout.Core.Interfaces;
using JobScout.Infrastructure.Data;
using JobScout.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JobScout.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class RatingsController : ControllerBase
{
    private const int RecalibrationRatingInterval = 20;

    private readonly JobScoutDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RatingsController> _logger;

    public RatingsController(
        JobScoutDbContext db,
        IServiceScopeFactory scopeFactory,
        ILogger<RatingsController> logger)
    {
        _db = db;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [HttpPost("ratings")]
    public async Task<ActionResult<UserRatingDto>> Create([FromBody] UserRatingRequest request)
    {
        if (request.Stars is < 1 or > 5)
            return BadRequest("Stars must be between 1 and 5.");

        var existing = await _db.UserRatings
            .FirstOrDefaultAsync(r => r.JobId == request.JobId && r.ProfileId == request.ProfileId);

        if (existing is not null)
        {
            existing.Stars = request.Stars;
            existing.Notes = request.Notes;
            existing.RatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(existing.ToDto());
        }

        var rating = new UserRating
        {
            Id = Guid.NewGuid(),
            JobId = request.JobId,
            ProfileId = request.ProfileId,
            Stars = request.Stars,
            Notes = request.Notes,
            RatedAt = DateTime.UtcNow
        };

        await _db.UserRatings.AddAsync(rating);
        await _db.SaveChangesAsync();

        await MaybeTriggerRecalibrationAsync(request.ProfileId);

        return CreatedAtAction(nameof(GetRatingForJob), new { id = rating.JobId, profileId = rating.ProfileId }, rating.ToDto());
    }

    private async Task MaybeTriggerRecalibrationAsync(Guid profileId)
    {
        var totalRatings = await _db.UserRatings.CountAsync(r => r.ProfileId == profileId);
        if (totalRatings == 0 || totalRatings % RecalibrationRatingInterval != 0)
            return;

        _logger.LogInformation(
            "Rating count reached {Count} for profile {ProfileId} — triggering soft recalibration",
            totalRatings, profileId);

        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var scoring = scope.ServiceProvider.GetRequiredService<IAiScoringService>();
            var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<RatingsController>>();
            try
            {
                await scoring.RecalibrateAsync(profileId, resetHistory: false);
            }
            catch (Exception ex)
            {
                scopedLogger.LogError(ex, "Auto-recalibration failed for profile {ProfileId}", profileId);
            }
        });
    }

    [HttpPut("ratings/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UserRatingRequest request)
    {
        if (request.Stars is < 1 or > 5)
            return BadRequest("Stars must be between 1 and 5.");

        var rating = await _db.UserRatings.FindAsync(id);
        if (rating is null)
            return NotFound();

        rating.Stars = request.Stars;
        rating.Notes = request.Notes;
        rating.RatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("jobs/{id:guid}/rating")]
    public async Task<ActionResult<UserRatingDto>> GetRatingForJob(Guid id, [FromQuery] Guid? profileId)
    {
        var query = _db.UserRatings.Where(r => r.JobId == id);
        if (profileId.HasValue)
            query = query.Where(r => r.ProfileId == profileId.Value);

        var rating = await query.FirstOrDefaultAsync();
        if (rating is null)
            return NotFound();

        return Ok(rating.ToDto());
    }
}
