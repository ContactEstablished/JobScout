using JobScout.Core.Enums;
using JobScout.Core.Models;

namespace JobScout.Core.Interfaces;

public interface INotificationService
{
    Task<Notification?> CreateAsync(
        string userId,
        NotificationType type,
        string title,
        string message,
        Guid? profileId = null,
        Guid? jobId = null,
        Guid? applicationId = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<Notification>> GetForUserAsync(
        string userId,
        bool unreadOnly = false,
        int take = 50,
        CancellationToken ct = default);

    Task<int> GetUnreadCountAsync(string userId, CancellationToken ct = default);
    Task<bool> MarkReadAsync(Guid notificationId, string userId, CancellationToken ct = default);
    Task<int> MarkAllReadAsync(string userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid notificationId, string userId, CancellationToken ct = default);

    Task OnIngestionCompleteAsync(SearchProfile profile, IngestionResult result, CancellationToken ct = default);
    Task OnHighScoreCreatedAsync(AiScore score, Job job, SearchProfile profile, CancellationToken ct = default);
    Task OnApplicationStatusChangedAsync(
        JobApplication application,
        ApplicationStatus oldStatus,
        ApplicationStatus newStatus,
        string userId,
        CancellationToken ct = default);
}
