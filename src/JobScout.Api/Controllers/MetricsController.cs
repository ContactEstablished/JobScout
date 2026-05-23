using JobScout.Core.DTOs;
using JobScout.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobScout.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class MetricsController : ControllerBase
{
    private readonly IMetricsService _metrics;

    public MetricsController(IMetricsService metrics) => _metrics = metrics;

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardStatsDto>> GetDashboard([FromQuery] Guid profileId)
        => Ok(await _metrics.GetDashboardStatsAsync(profileId));

    [HttpGet("by-source")]
    public async Task<ActionResult<IReadOnlyList<SourceBreakdownDto>>> GetBySource([FromQuery] Guid profileId)
        => Ok(await _metrics.GetSourceBreakdownAsync(profileId));

    [HttpGet("posting-windows")]
    public async Task<ActionResult<IReadOnlyList<PostingWindowDto>>> GetPostingWindows([FromQuery] Guid profileId)
        => Ok(await _metrics.GetPostingWindowsAsync(profileId));

    [HttpGet("trends")]
    public async Task<ActionResult<IReadOnlyList<DashboardStatsDto>>> GetTrends(
        [FromQuery] Guid profileId,
        [FromQuery] int days = 30)
        => Ok(await _metrics.GetTrendsAsync(profileId, days));

    [HttpGet("ai-costs")]
    public async Task<ActionResult<AiCostSummaryDto>> GetAiCosts([FromQuery] Guid? profileId = null)
        => Ok(await _metrics.GetAiCostSummaryAsync(profileId));
}
