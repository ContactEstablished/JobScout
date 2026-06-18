namespace JobScout.Web.Services;

public class JobsFilter
{
    public string? Source { get; set; }
    public decimal? MinScore { get; set; }
    public string? LocationType { get; set; }
    public string? JobType { get; set; }
    public string? Query { get; set; }
    public string? SortBy { get; set; }
}
