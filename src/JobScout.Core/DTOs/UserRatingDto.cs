namespace JobScout.Core.DTOs;

public class UserRatingRequest
{
    public Guid JobId { get; set; }
    public Guid ProfileId { get; set; }
    public int Stars { get; set; }
    public string? Notes { get; set; }
}

public class UserRatingDto
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid ProfileId { get; set; }
    public int Stars { get; set; }
    public string? Notes { get; set; }
    public DateTime RatedAt { get; set; }
}
