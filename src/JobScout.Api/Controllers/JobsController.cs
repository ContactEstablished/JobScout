using JobScout.Api.Mapping;
using JobScout.Core.DTOs;
using JobScout.Core.Enums;
using JobScout.Core.Interfaces;
using JobScout.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JobScout.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly IJobRepository _jobs;
    private readonly JobScoutDbContext _db;

    public JobsController(IJobRepository jobs, JobScoutDbContext db)
    {
        _jobs = jobs;
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<JobSummaryDto>>> GetJobs(
        [FromQuery] Guid? profileId,
        [FromQuery] JobSource? source,
        [FromQuery] decimal? minScore,
        [FromQuery] LocationType? locationType,
        [FromQuery] JobType? jobType,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (profileId is null)
            return BadRequest("profileId is required.");

        var filters = new JobFilterOptions
        {
            Source = source,
            MinScore = minScore,
            LocationType = locationType,
            JobType = jobType,
            Query = q
        };

        var (items, totalCount) = await _jobs.GetByProfileAsync(profileId.Value, filters, page, pageSize);

        return Ok(new PagedResult<JobSummaryDto>
        {
            Items = items.Select(j => j.ToSummaryDto(profileId.Value)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JobDetailDto>> GetJob(Guid id, [FromQuery] Guid? profileId)
    {
        var job = await _jobs.GetByIdAsync(id);
        if (job is null)
            return NotFound();

        return Ok(job.ToDetailDto(profileId));
    }

    [HttpGet("{id:guid}/score")]
    public async Task<ActionResult<AiScoreDto>> GetScore(Guid id, [FromQuery] Guid? profileId)
    {
        var query = _db.AiScores.Where(s => s.JobId == id);
        if (profileId.HasValue)
            query = query.Where(s => s.ProfileId == profileId.Value);

        var score = await query.OrderByDescending(s => s.ScoredAt).FirstOrDefaultAsync();
        if (score is null)
            return NotFound();

        return Ok(score.ToDto());
    }
}
