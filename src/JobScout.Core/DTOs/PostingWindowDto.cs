namespace JobScout.Core.DTOs;

public class PostingWindowDto
{
    public DayOfWeek DayOfWeek { get; set; }
    public string DayName { get; set; } = string.Empty;
    public int JobCount { get; set; }
    public string BestTimeWindow { get; set; } = string.Empty;
}
