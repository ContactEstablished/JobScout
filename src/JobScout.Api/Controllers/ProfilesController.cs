using JobScout.Api.Mapping;
using JobScout.Core.DTOs;
using JobScout.Core.Interfaces;
using JobScout.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace JobScout.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProfilesController : ControllerBase
{
    private readonly IProfileRepository _profiles;

    public ProfilesController(IProfileRepository profiles) => _profiles = profiles;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SearchProfileDto>>> GetAll()
    {
        var profiles = await _profiles.GetAllAsync();
        return Ok(profiles.Select(p => p.ToDto()));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SearchProfileDto>> GetById(Guid id)
    {
        var profile = await _profiles.GetByIdAsync(id);
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
            CreatedAt = now,
            UpdatedAt = now,
            IsActive = false
        };

        await _profiles.AddAsync(profile);
        return CreatedAtAction(nameof(GetById), new { id = profile.Id }, profile.ToDto());
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProfileRequest request)
    {
        var profile = await _profiles.GetByIdAsync(id);
        if (profile is null)
            return NotFound();

        profile.Name = request.Name;
        profile.Description = request.Description;
        profile.LinkedInUrl = request.LinkedInUrl;
        profile.IsActive = request.IsActive;
        profile.UpdatedAt = DateTime.UtcNow;

        await _profiles.UpdateAsync(profile);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var profile = await _profiles.GetByIdAsync(id);
        if (profile is null)
            return NotFound();

        await _profiles.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("{id:guid}/resume")]
    public async Task<IActionResult> UploadResume(Guid id, IFormFile file)
    {
        var profile = await _profiles.GetByIdAsync(id);
        if (profile is null)
            return NotFound();

        if (file.Length == 0)
            return BadRequest("File is empty.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".txt" or ".docx" or ".pdf"))
            return BadRequest("Only .txt, .docx, and .pdf files are supported.");

        string resumeText;
        if (extension == ".txt")
        {
            using var reader = new StreamReader(file.OpenReadStream());
            resumeText = await reader.ReadToEndAsync();
        }
        else
        {
            // DOCX/PDF parsing is implemented in Phase 9 (ResumeParser).
            // Store filename now; text extraction will be wired up then.
            resumeText = string.Empty;
        }

        profile.ResumeFileName = file.FileName;
        profile.ResumeText = resumeText;
        profile.UpdatedAt = DateTime.UtcNow;

        await _profiles.UpdateAsync(profile);
        return NoContent();
    }

    [HttpPost("{id:guid}/recalibrate")]
    public async Task<IActionResult> Recalibrate(Guid id, [FromBody] RecalibrateRequest request)
    {
        var profile = await _profiles.GetByIdAsync(id);
        if (profile is null)
            return NotFound();

        // IAiScoringService.RecalibrateAsync is wired in Phase 5.
        // Return 202 Accepted so the Blazor UI can call this endpoint immediately.
        return Accepted(new { message = "Recalibration queued. AI scoring service will be wired in Phase 5." });
    }
}
