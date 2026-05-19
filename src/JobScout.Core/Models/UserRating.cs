namespace JobScout.Core.Models;

public class UserRating
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid ProfileId { get; set; }
    public int Stars { get; set; }
    public string? Notes { get; set; }
    public DateTime RatedAt { get; set; }

    public Job Job { get; set; } = null!;
    public SearchProfile Profile { get; set; } = null!;
}
