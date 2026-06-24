using BankWebApp.Web.Data;
using BankWebApp.Web.DTOs.Notifications;
using BankWebApp.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BankWebApp.Web.Services.Implementations;

public class NotificationService : INotificationService
{
    private readonly IDbContextFactory<BankDbContext> _dbContextFactory;

    public NotificationService(IDbContextFactory<BankDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<List<NotificationDto>> GetRecentNotificationsAsync(
        long userId,
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await BuildNotificationQuery(dbContext, userId)
            .Take(Math.Clamp(count, 1, 20))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<NotificationDto>> GetRecentUnreadNotificationsAsync(
        long userId,
        int count = 5,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await BuildNotificationQuery(dbContext, userId)
            .Where(notification => !notification.IsRead)
            .Take(Math.Clamp(count, 1, 10))
            .ToListAsync(cancellationToken);
    }

    public async Task MarkAsReadAsync(
        long userId,
        long notificationId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var notification = await dbContext.Notifications
            .FirstOrDefaultAsync(item => item.Id == notificationId && item.UserId == userId, cancellationToken);

        if (notification is null || notification.IsRead)
        {
            return;
        }

        notification.IsRead = true;
        notification.ReadAt = BankWebApp.Web.Helpers.MongoliaClock.Now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<NotificationDto> BuildNotificationQuery(BankDbContext dbContext, long userId)
    {
        return dbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId)
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Select(notification => new NotificationDto
            {
                Id = notification.Id,
                TransactionId = notification.TransactionId,
                NotificationType = notification.NotificationType,
                Title = notification.Title,
                Message = notification.Message,
                IsRead = notification.IsRead,
                CreatedAt = notification.CreatedAt
            });
    }
}
