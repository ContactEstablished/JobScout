using JobScout.Core.Enums;

namespace JobScout.Core.Models;

public class DailyMetric
{
    public Guid Id { get; set; }
    public Guid ProfileId { get; set; }
    public DateOnly Date { get; set; }
    public JobSource Source { get; set; }
    public int JobsFound { get; set; }
    public int StrongFits { get; set; }
    public int UserLiked { get; set; }
    public int Applied { get; set; }

    public SearchProfile Profile { get; set; } = null!;
}
