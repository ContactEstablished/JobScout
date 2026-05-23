using JobScout.Api.Mapping;
using JobScout.Core.DTOs;
using JobScout.Core.Interfaces;
using JobScout.Core.Models;
using JobScout.Infrastructure.Parsing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobScout.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ProfilesController : ControllerBase
{
    private readonly IProfileRepository _profiles;
    private readonly IAiScoringService _scoring;
    private readonly IJobIngestionService _ingestion;
    private readonly ICurrentUserService _currentUser;

    public ProfilesController(
        IProfileRepository profiles,
        IAiScoringService scoring,
        IJobIngestionService ingestion,
        ICurrentUserService currentUser)
    {
        _profiles    = profiles;
        _scoring     = scoring;
        _ingestion   = ingestion;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SearchProfileDto>>> GetAll()
    {
        var profiles = await _profiles.GetAllAsync(_currentUser.UserId);
        return Ok(profiles.Select(p => p.ToDto()));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SearchProfileDto>> GetById(Guid id)
    {
        var profile = await _profiles.GetByIdAsync(id, _currentUser.UserId);
        if (profile is null)
            return NotFound();

        return Ok(profile.ToDto());
    }

    [HttpPost]
    public async Task<ActionResult<SearchProfileDto>> Create([FromBody] CreateProfileRequest request)
    {
        var now = DateTime.UtcNow;
        var profile = new SearchProfile
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            LinkedInUrl = request.LinkedInUrl,
            UserId = _currentUser.UserId,
            CreatedAt = now,
            UpdatedAt = now,
            IsActive = false,
            SearchKeywords = request.SearchKeywords,
            PreferredSources = request.PreferredSources,
            PreferredJobTypes = request.PreferredJobTypes,
            PreferredLocationTypes = request.PreferredLocationTypes,
            LocationPreference = request.LocationPreference,
            ProfileColor = request.ProfileColor
        };

        await _profiles.AddAsync(profile);
        return CreatedAtAction(nameof(GetById), new { id = profile.Id }, profile.ToDto());
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProfileRequest request)
    {
        var profile = await _profiles.GetByIdAsync(id, _currentUser.UserId);
        if (profile is null)
            return NotFound();

        profile.Name = request.Name;
        profile.Description = request.Description;
        profile.LinkedInUrl = request.LinkedInUrl;
        profile.IsActive = request.IsActive;
        profile.SearchKeywords = request.SearchKeywords;
        profile.PreferredSources = request.PreferredSources;
        profile.PreferredJobTypes = request.PreferredJobTypes;
        profile.PreferredLocationTypes = request.PreferredLocationTypes;
        profile.LocationPreference = request.LocationPreference;
        profile.ProfileColor = request.ProfileColor;
        profile.UpdatedAt = DateTime.UtcNow;

        await _profiles.UpdateAsync(profile);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var profile = await _profiles.GetByIdAsync(id, _currentUser.UserId);
        if (profile is null)
            return NotFound();

        await _profiles.DeleteAsync(id, _currentUser.UserId);
        return NoContent();
    }

    [HttpPost("{id:guid}/resume")]
    public async Task<IActionResult> UploadResume(Guid id, IFormFile file)
    {
        var profile = await _profiles.GetByIdAsync(id, _currentUser.UserId);
        if (profile is null)
            return NotFound();

        if (file.Length == 0)
            return BadRequest("File is empty.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".txt" or ".docx" or ".pdf"))
            return BadRequest("Only .txt, .docx, and .pdf files are supported.");

        var parsed = await ResumeParser.ParseAsync(file.OpenReadStream(), extension);

        profile.ResumeFileName = file.FileName;
        profile.ResumeText = parsed.PlainText;
        profile.DetectedSkills = [.. parsed.DetectedSkills];
        profile.UpdatedAt = DateTime.UtcNow;

        await _profiles.UpdateAsync(profile);
        return Ok(new { parsed.WordCount, parsed.DetectedSkills });
    }

    [HttpPost("{id:guid}/recalibrate")]
    public async Task<IActionResult> Recalibrate(Guid id, [FromBody] RecalibrateRequest request)
    {
        var profile = await _profiles.GetByIdAsync(id, _currentUser.UserId);
        if (profile is null)
            return NotFound();

        await _scoring.RecalibrateAsync(id, request.ResetHistory);
        return Accepted(new { message = "Recalibration complete." });
    }

    [HttpPost("{id:guid}/ingest")]
    public async Task<IActionResult> Ingest(Guid id)
    {
        var profile = await _profiles.GetByIdAsync(id, _currentUser.UserId);
        if (profile is null)
            return NotFound();

        var result = await _ingestion.IngestAsync(profile);
        return Ok(result);
    }

    [HttpPut("{id:guid}/skills")]
    public async Task<IActionResult> UpdateSkills(Guid id, [FromBody] UpdateSkillsRequest request)
    {
        var profile = await _profiles.GetByIdAsync(id, _currentUser.UserId);
        if (profile is null)
            return NotFound();

        profile.DetectedSkills = request.Skills;
        profile.UpdatedAt = DateTime.UtcNow;

        await _profiles.UpdateAsync(profile);
        return Ok(new { skills = profile.DetectedSkills });
    }

    [HttpPost("{id:guid}/clone")]
    public async Task<ActionResult<SearchProfileDto>> Clone(Guid id)
    {
        var source = await _profiles.GetByIdAsync(id, _currentUser.UserId);
        if (source is null)
            return NotFound();

        var now = DateTime.UtcNow;
        var clone = new SearchProfile
        {
            Id = Guid.NewGuid(),
            Name = $"{source.Name} (Copy)",
            Description = source.Description,
            LinkedInUrl = source.LinkedInUrl,
            ResumeText = source.ResumeText,
            ResumeFileName = source.ResumeFileName,
            UserId = _currentUser.UserId,
            CreatedAt = now,
            UpdatedAt = now,
            IsActive = false,
            SearchKeywords = [.. source.SearchKeywords],
            PreferredSources = [.. source.PreferredSources],
            PreferredJobTypes = [.. source.PreferredJobTypes],
            PreferredLocationTypes = [.. source.PreferredLocationTypes],
            LocationPreference = source.LocationPreference,
            DetectedSkills = [.. source.DetectedSkills],
            ProfileColor = source.ProfileColor
        };

        await _profiles.AddAsync(clone);
        return CreatedAtAction(nameof(GetById), new { id = clone.Id }, clone.ToDto());
    }
}
