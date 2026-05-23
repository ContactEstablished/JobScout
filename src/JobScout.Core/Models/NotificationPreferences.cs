namespace JobScout.Core.Models;

public class NotificationPreferences
{
    public string UserId { get; set; } = string.Empty;

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
