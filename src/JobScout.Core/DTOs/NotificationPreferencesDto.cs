namespace JobScout.Core.DTOs;

public class NotificationPreferencesDto
{
    public bool InAppNewStrongFit { get; set; } = true;
    public bool InAppScoreUpdate { get; set; } = true;
    public bool InAppIngestionComplete { get; set; } = true;
    public bool InAppApplicationStatusChange { get; set; } = true;

    public bool EmailDailyDigest { get; set; }
    public bool EmailWeeklySummary { get; set; }
    public bool EmailInstantStrongMatch { get; set; }

    public TimeOnly? QuietHoursStart { get; set; }
    public TimeOnly? QuietHoursEnd { get; set; }
    public string TimeZoneId { get; set; } = "UTC";
}
