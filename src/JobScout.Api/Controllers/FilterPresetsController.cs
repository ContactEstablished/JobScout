using JobScout.Api.Mapping;
using JobScout.Core.DTOs;
using JobScout.Core.Interfaces;
using JobScout.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobScout.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class FilterPresetsController : ControllerBase
{
    private readonly IFilterPresetRepository _presets;
    private readonly IProfileRepository _profiles;
    private readonly ICurrentUserService _currentUser;

    public FilterPresetsController(
        IFilterPresetRepository presets,
        IProfileRepository profiles,
        ICurrentUserService currentUser)
    {
        _presets = presets;
        _profiles = profiles;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FilterPresetDto>>> GetByProfile([FromQuery] Guid profileId)
    {
        if (!await OwnsProfileAsync(profileId))
            return NotFound();

        var presets = await _presets.GetByProfileAsync(profileId);
        return Ok(presets.Select(p => p.ToDto()).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<FilterPresetDto>> Create([FromBody] SaveFilterPresetRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("A preset name is required.");

        if (!await OwnsProfileAsync(request.ProfileId))
            return NotFound();

        var existing = await _presets.GetByProfileAsync(request.ProfileId);
        if (existing.Any(p => string.Equals(p.Name, request.Name, StringComparison.OrdinalIgnoreCase)))
            return Conflict(new { message = "A preset with this name already exists for this profile." });

        var preset = new FilterPreset
        {
            Id = Guid.NewGuid(),
            ProfileId = request.ProfileId,
            Name = request.Name.Trim(),
            Source = request.Source,
            MinScore = request.MinScore,
            LocationType = request.LocationType,
            JobType = request.JobType,
            Query = request.Query,
            SortBy = request.SortBy,
            CreatedAt = DateTime.UtcNow
        };

        await _presets.AddAsync(preset);
        return CreatedAtAction(nameof(GetByProfile), new { profileId = preset.ProfileId }, preset.ToDto());
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveFilterPresetRequest request)
    {
        var preset = await _presets.GetByIdAsync(id);
        if (preset is null || !await OwnsProfileAsync(preset.ProfileId))
            return NotFound();

        preset.Name = request.Name.Trim();
        preset.Source = request.Source;
        preset.MinScore = request.MinScore;
        preset.LocationType = request.LocationType;
        preset.JobType = request.JobType;
        preset.Query = request.Query;
        preset.SortBy = request.SortBy;

        await _presets.UpdateAsync(preset);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var preset = await _presets.GetByIdAsync(id);
        if (preset is null || !await OwnsProfileAsync(preset.ProfileId))
            return NotFound();

        await _presets.DeleteAsync(id);
        return NoContent();
    }

    // A preset is reachable only through a profile the caller owns.
    private async Task<bool> OwnsProfileAsync(Guid profileId) =>
        await _profiles.GetByIdAsync(profileId, _currentUser.UserId) is not null;
}
