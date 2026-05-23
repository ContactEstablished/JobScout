using JobScout.Core.Enums;

namespace JobScout.Core.DTOs;

public class NotificationDto
{
    public Guid Id { get; set; }
    public Guid? ProfileId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public Guid? RelatedJobId { get; set; }
    public Guid? RelatedApplicationId { get; set; }
}

public class UnreadCountDto
{
    public int Count { get; set; }
}
