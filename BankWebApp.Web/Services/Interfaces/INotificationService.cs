using BankWebApp.Web.DTOs.Notifications;

namespace BankWebApp.Web.Services.Interfaces;

public interface INotificationService
{
    Task<List<NotificationDto>> GetRecentUnreadNotificationsAsync(
        long userId,
        int count = 5,
        CancellationToken cancellationToken = default);
}
