namespace JobScout.Core.DTOs;

public class DashboardStatsDto
{
    public DateOnly? Date { get; set; }
    public int JobsFound { get; set; }
    public int StrongFits { get; set; }
    public int Saved { get; set; }
    public int Applied { get; set; }
    public int JobsFoundDelta { get; set; }
    public int StrongFitsDelta { get; set; }
    public int SavedDelta { get; set; }
    public int AppliedDelta { get; set; }
}
