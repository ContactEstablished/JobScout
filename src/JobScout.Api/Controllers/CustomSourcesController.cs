using JobScout.Api.Mapping;
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
[Route("api/custom-sources")]
public class CustomSourcesController(
    JobScoutDbContext db,
    ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomJobSourceDto>>> GetByProfile([FromQuery] Guid profileId)
    {
        var sources = await db.CustomJobSources
            .Include(s => s.Profile)
            .Where(s => s.ProfileId == profileId && s.Profile.UserId == currentUser.UserId)
            .ToListAsync();

        return Ok(sources.Select(s => s.ToDto()));
    }

    [HttpPost]
    public async Task<ActionResult<CustomJobSourceDto>> Create([FromBody] CreateCustomJobSourceRequest request)
    {
        // Verify the profile belongs to the current user
        var profile = await db.SearchProfiles
            .FirstOrDefaultAsync(p => p.Id == request.ProfileId && p.UserId == currentUser.UserId);

        if (profile is null)
            return NotFound(new { message = "Profile not found." });

        var source = new CustomJobSource
        {
            Id                  = Guid.NewGuid(),
            ProfileId           = request.ProfileId,
            Name                = request.Name,
            FeedUrl             = request.FeedUrl,
            Format              = request.Format,
            IsActive            = true,
            CreatedAt           = DateTimeOffset.UtcNow,
            JsonJobsPath        = request.JsonJobsPath,
            JsonTitleField      = request.JsonTitleField,
            JsonCompanyField    = request.JsonCompanyField,
            JsonLocationField   = request.JsonLocationField,
            JsonDescriptionField = request.JsonDescriptionField,
            JsonUrlField        = request.JsonUrlField,
            JsonPostedAtField   = request.JsonPostedAtField
        };

        db.CustomJobSources.Add(source);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetByProfile), new { profileId = source.ProfileId }, source.ToDto());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var source = await db.CustomJobSources
            .Include(s => s.Profile)
            .FirstOrDefaultAsync(s => s.Id == id && s.Profile.UserId == currentUser.UserId);

        if (source is null)
            return NotFound();

        db.CustomJobSources.Remove(source);
        await db.SaveChangesAsync();

        return NoContent();
    }
}
