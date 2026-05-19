using JobScout.Core.Models;

namespace JobScout.Core.Interfaces;

public class IngestionResult
{
    public int NewJobsFound { get; set; }
    public int Duplicates { get; set; }
    public Dictionary<string, int> BySource { get; set; } = [];
}

public interface IJobIngestionService
{
    Task<IngestionResult> IngestAsync(SearchProfile profile);
}
