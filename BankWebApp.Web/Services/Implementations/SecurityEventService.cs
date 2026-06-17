using BankWebApp.Web.Data;
using BankWebApp.Web.Data.Entities;
using BankWebApp.Web.Helpers;
using BankWebApp.Web.Services.Interfaces;

namespace BankWebApp.Web.Services.Implementations;

public class SecurityEventService : ISecurityEventService
{
    private readonly BankDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDatabaseClockService _databaseClockService;

    public SecurityEventService(
        BankDbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        IDatabaseClockService databaseClockService)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _databaseClockService = databaseClockService;
    }

    public async Task LogAsync(
        long? userId,
        string? usernameOrEmail,
        string eventType,
        bool success,
        string? message,
        CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var userAgent = httpContext?.Request.Headers.UserAgent.ToString();
        var clock = await _databaseClockService.GetSnapshotAsync(cancellationToken);

        _dbContext.SecurityEventLogs.Add(new SecurityEventLog
        {
            UserId = userId,
            UsernameOrEmail = string.IsNullOrWhiteSpace(usernameOrEmail) ? null : usernameOrEmail.Trim(),
            EventType = eventType,
            Success = success,
            Message = message,
            IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = userAgent is { Length: > 255 } ? userAgent[..255] : userAgent,
            CreatedAtUtc = clock.UtcNow,
            CreatedAt = MongoliaClock.ToMongoliaTime(clock.UtcNow)
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
