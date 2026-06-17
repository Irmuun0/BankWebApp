using BankWebApp.Web.Data;
using BankWebApp.Web.DTOs.Notifications;
using BankWebApp.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BankWebApp.Web.Services.Implementations;

public class NotificationService : INotificationService
{
    private readonly BankDbContext _dbContext;

    public NotificationService(BankDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<NotificationDto>> GetRecentUnreadNotificationsAsync(
        long userId,
        int count = 5,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId && !notification.IsRead)
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Take(Math.Clamp(count, 1, 10))
            .Select(notification => new NotificationDto
            {
                Id = notification.Id,
                TransactionId = notification.TransactionId,
                NotificationType = notification.NotificationType,
                Title = notification.Title,
                Message = notification.Message,
                IsRead = notification.IsRead,
                CreatedAt = notification.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }
}
