using JobScout.Api.Mapping;
using JobScout.Core.DTOs;
using JobScout.Infrastructure.Data;
using JobScout.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JobScout.Api.Controllers;

[ApiController]
[Route("api")]
public class RatingsController : ControllerBase
{
    private readonly JobScoutDbContext _db;

    public RatingsController(JobScoutDbContext db) => _db = db;

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
        return CreatedAtAction(nameof(GetRatingForJob), new { id = rating.JobId, profileId = rating.ProfileId }, rating.ToDto());
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
