using System.Text.Json;
using BankWebApp.Web.Data;
using BankWebApp.Web.Data.Entities;
using BankWebApp.Web.DTOs.Admin;
using BankWebApp.Web.Helpers;
using BankWebApp.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BankWebApp.Web.Services.Implementations;

public partial class AdminService : IAdminService
{
    private static readonly string[] ChartColors =
    [
        "#dc2626",
        "#f59e0b",
        "#2563eb",
        "#16a34a",
        "#7c3aed",
        "#0891b2",
        "#64748b"
    ];

    private readonly BankDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IGeminiAnalysisService _geminiAnalysisService;

    public AdminService(
        BankDbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        IGeminiAnalysisService geminiAnalysisService)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _geminiAnalysisService = geminiAnalysisService;
    }

    // Бусад үйлдэл AdminService.<үүрэг>.cs файлд байна; доор нийтлэг helper-үүд үлдсэн.
    public async Task<AdminDashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken cancellationToken = default)
    {
        var todayStart = MongoliaClock.Today.ToDateTime(TimeOnly.MinValue);
        var tomorrowStart = todayStart.AddDays(1);

        return new AdminDashboardSummaryDto
        {
            TotalUsers = await _dbContext.Users.CountAsync(cancellationToken),
            ActiveUsers = await _dbContext.Users.CountAsync(user => user.IsActive, cancellationToken),
            InactiveUsers = await _dbContext.Users.CountAsync(user => !user.IsActive, cancellationToken),
            TotalAccounts = await _dbContext.Accounts.CountAsync(cancellationToken),
            ActiveAccounts = await _dbContext.Accounts.CountAsync(account => account.IsActive, cancellationToken),
            TotalTransactions = await _dbContext.Transactions.CountAsync(cancellationToken),
            SuspiciousTransactions = await _dbContext.Transactions.CountAsync(transaction => transaction.IsSuspicious, cancellationToken),
            PendingReviews = await _dbContext.SuspiciousTransactionDetails.CountAsync(detail => detail.ReviewStatus == "PENDING", cancellationToken),
            TodayTransactions = await _dbContext.Transactions.CountAsync(transaction => transaction.CreatedAt >= todayStart && transaction.CreatedAt < tomorrowStart, cancellationToken),
            TodaySuspiciousTransactions = await _dbContext.Transactions.CountAsync(transaction => transaction.IsSuspicious && transaction.CreatedAt >= todayStart && transaction.CreatedAt < tomorrowStart, cancellationToken),
            TodayFxIncomeMnt = await _dbContext.FxIncomeLogs
                .Where(log => log.CreatedAt >= todayStart && log.CreatedAt < tomorrowStart)
                .SumAsync(log => (decimal?)log.IncomeAmountMnt, cancellationToken) ?? 0m
        };
    }

    private static string BuildUserDisplayName(User user)
    {
        return UserDisplayNameFormatter.Format(user.FirstName, user.LastName, user.Username);
    }

    private void AddAuditLog(
        long adminUserId,
        string action,
        string targetType,
        long targetId,
        object oldValue,
        object newValue,
        string detail)
    {
        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            OldValue = JsonSerializer.Serialize(oldValue),
            NewValue = JsonSerializer.Serialize(newValue),
            Detail = detail,
            IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString(),
            CreatedAt = MongoliaClock.Now
        });
    }

    private static string? NormalizeSearch(string? search)
    {
        var normalized = search?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static (int Page, int PageSize) NormalizePage(int page, int pageSize, int totalItems)
    {
        var normalizedPageSize = Math.Clamp(pageSize, 5, 100);
        var totalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)normalizedPageSize);
        var normalizedPage = Math.Clamp(page, 1, totalPages);
        return (normalizedPage, normalizedPageSize);
    }
}
