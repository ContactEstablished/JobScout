using JobScout.Api.Mapping;
using JobScout.Core.DTOs;
using JobScout.Core.Enums;
using JobScout.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobScout.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ApplicationsController : ControllerBase
{
    private readonly IApplicationTrackingService _tracking;
    private readonly IApplicationRepository _applications;
    private readonly ICurrentUserService _currentUser;

    public ApplicationsController(
        IApplicationTrackingService tracking,
        IApplicationRepository applications,
        ICurrentUserService currentUser)
    {
        _tracking     = tracking;
        _applications = applications;
        _currentUser  = currentUser;
    }

    [HttpPost]
    public async Task<ActionResult<ApplicationDto>> Create([FromBody] CreateApplicationRequest request)
    {
        try
        {
            var app = await _tracking.ApplyAsync(
                request.JobId, request.ProfileId, _currentUser.UserId, request.Notes);
            return CreatedAtAction(nameof(GetByProfile), null, app.ToDto());
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
        {
            return Conflict(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ApplicationDto>>> GetByProfile(
        [FromQuery] Guid profileId,
        [FromQuery] ApplicationStatus? status = null)
    {
        var apps = await _applications.GetByProfileAsync(profileId, _currentUser.UserId, status);
        return Ok(apps.Select(a => a.ToDto()));
    }

    [HttpGet("pipeline")]
    public async Task<ActionResult<PipelineDto>> GetPipeline([FromQuery] Guid profileId)
    {
        var pipeline = await _tracking.GetPipelineAsync(profileId, _currentUser.UserId);
        return Ok(pipeline);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<ApplicationDto>> UpdateStatus(
        Guid id, [FromBody] UpdateStatusRequest request)
    {
        try
        {
            var app = await _tracking.UpdateStatusAsync(
                id, request.Status, _currentUser.UserId, request.Notes);
            return Ok(app.ToDto());
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Cannot transition"))
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var app = await _applications.GetByIdAsync(id, _currentUser.UserId);
        if (app is null)
            return NotFound();

        await _applications.DeleteAsync(id, _currentUser.UserId);
        return NoContent();
    }
}
