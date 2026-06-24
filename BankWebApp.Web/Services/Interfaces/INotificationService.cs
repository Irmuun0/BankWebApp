using BankWebApp.Web.DTOs.Notifications;

namespace BankWebApp.Web.Services.Interfaces;

public interface INotificationService
{
    Task<List<NotificationDto>> GetRecentNotificationsAsync(
        long userId,
        int count = 10,
        CancellationToken cancellationToken = default);

    Task<List<NotificationDto>> GetRecentUnreadNotificationsAsync(
        long userId,
        int count = 5,
        CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(
        long userId,
        long notificationId,
        CancellationToken cancellationToken = default);
}
