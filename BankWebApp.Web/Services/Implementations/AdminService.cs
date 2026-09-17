using System.Text.Json;
using BankWebApp.Web.Components.Charts;
using BankWebApp.Web.Data;
using BankWebApp.Web.Data.Entities;
using BankWebApp.Web.DTOs.Admin;
using BankWebApp.Web.DTOs.Ai;
using BankWebApp.Web.Helpers;
using BankWebApp.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BankWebApp.Web.Services.Implementations;

public class AdminService : IAdminService
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

    public async Task<AdminPagedResultDto<AdminUserDto>> GetUsersAsync(
        AdminUserFilterDto? filter = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Users.AsNoTracking();
        var normalizedSearch = NormalizeSearch(filter?.Search);
        if (normalizedSearch is not null)
        {
            query = query.Where(user =>
                user.Username.Contains(normalizedSearch) ||
                user.Email.Contains(normalizedSearch) ||
                user.PhoneNumber.Contains(normalizedSearch) ||
                user.Role.Contains(normalizedSearch) ||
                ((user.FirstName ?? "") + " " + (user.LastName ?? "")).Contains(normalizedSearch) ||
                ((user.LastName ?? "") + "-ийн " + (user.FirstName ?? "")).Contains(normalizedSearch));
        }

        var username = NormalizeSearch(filter?.Username);
        if (username is not null)
        {
            query = query.Where(user => user.Username.Contains(username));
        }

        var email = NormalizeSearch(filter?.Email);
        if (email is not null)
        {
            query = query.Where(user => user.Email.Contains(email));
        }

        var name = NormalizeSearch(filter?.Name);
        if (name is not null)
        {
            query = query.Where(user =>
                ((user.FirstName ?? "") + " " + (user.LastName ?? "")).Contains(name) ||
                ((user.LastName ?? "") + " " + (user.FirstName ?? "")).Contains(name) ||
                ((user.LastName ?? "") + "-ийн " + (user.FirstName ?? "")).Contains(name));
        }

        var role = NormalizeSearch(filter?.Role)?.ToUpperInvariant();
        if (role is not null && role is not "ALL")
        {
            query = query.Where(user => user.Role == role);
        }

        var status = NormalizeSearch(filter?.Status)?.ToUpperInvariant();
        if (status is "ACTIVE")
        {
            query = query.Where(user => user.IsActive);
        }
        else if (status is "INACTIVE")
        {
            query = query.Where(user => !user.IsActive);
        }

        if (filter?.CreatedFrom is not null)
        {
            var start = filter.CreatedFrom.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(user => user.CreatedAt >= start);
        }

        if (filter?.CreatedTo is not null)
        {
            var endExclusive = filter.CreatedTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(user => user.CreatedAt < endExclusive);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var pageInfo = NormalizePage(page, pageSize, totalItems);
        var users = await query
            .OrderByDescending(user => user.CreatedAt)
            .ThenByDescending(user => user.Id)
            .Skip((pageInfo.Page - 1) * pageInfo.PageSize)
            .Take(pageInfo.PageSize)
            .Select(user => new AdminUserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                FullName = user.LastName == null || user.LastName == ""
                    ? (user.FirstName ?? "")
                    : user.FirstName == null || user.FirstName == ""
                        ? user.LastName + "-ийн"
                        : user.LastName + "-ийн " + user.FirstName,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt
            })
            .ToListAsync(cancellationToken);

        foreach (var user in users.Where(user => string.IsNullOrWhiteSpace(user.FullName)))
        {
            user.FullName = null;
        }

        return new AdminPagedResultDto<AdminUserDto>
        {
            Items = users,
            Page = pageInfo.Page,
            PageSize = pageInfo.PageSize,
            TotalItems = totalItems
        };
    }

    public async Task<AdminUserProfileDto?> GetUserProfileAsync(long userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => new AdminUserProfileDto
            {
                Id = item.Id,
                Username = item.Username,
                Email = item.Email,
                FirstName = item.FirstName,
                LastName = item.LastName,
                PhoneNumber = item.PhoneNumber,
                EmergencyPhoneNumber = item.EmergencyPhoneNumber,
                Role = item.Role,
                IsActive = item.IsActive,
                PasswordResetRequired = item.PasswordResetRequired,
                FailedLoginCount = item.FailedLoginCount,
                LockedUntil = item.LockedUntil,
                LastLoginAt = item.LastLoginAt,
                PasswordChangedAt = item.PasswordChangedAt,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return null;
        }

        user.Accounts = await _dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.UserId == userId)
            .OrderByDescending(account => account.IsPrimary)
            .ThenByDescending(account => account.CreatedAt)
            .Select(account => new AdminUserAccountSummaryDto
            {
                Id = account.Id,
                AccountNumber = account.AccountNumber,
                AccountType = account.AccountType,
                Currency = account.Currency,
                Balance = account.Balance,
                IsActive = account.IsActive,
                IsPrimary = account.IsPrimary,
                OpenedAt = account.CreatedAt
            })
            .ToListAsync(cancellationToken);

        user.RecentAuditLogs = await _dbContext.AuditLogs
            .AsNoTracking()
            .Include(log => log.User)
            .Where(log => log.TargetType == "users" && log.TargetId == userId)
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .Take(8)
            .Select(log => new AdminUserProfileAuditDto
            {
                Id = log.Id,
                Action = log.Action,
                Detail = log.Detail ?? string.Empty,
                NewValue = log.NewValue,
                ActorUsername = log.User == null ? null : log.User.Username,
                CreatedAt = log.CreatedAt
            })
            .ToListAsync(cancellationToken);

        foreach (var log in user.RecentAuditLogs)
        {
            log.ActionLabel = GetAuditActionLabel(log.Action);
            log.Detail = BuildAuditDisplayDetail(log.Action, log.Detail, log.NewValue);
        }

        return user;
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateUserProfileAsync(
        long adminUserId,
        UpdateAdminUserProfileDto dto,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(item => item.Id == dto.UserId, cancellationToken);
        if (user is null)
        {
            return (false, "Хэрэглэгч олдсонгүй.");
        }

        var username = dto.Username.Trim();
        var email = dto.Email.Trim();
        var firstName = NormalizeOptional(dto.FirstName);
        var lastName = NormalizeOptional(dto.LastName);
        var phoneNumber = dto.PhoneNumber.Trim();
        var emergencyPhone = NormalizeOptional(dto.EmergencyPhoneNumber);

        if (username.Length < 3)
        {
            return (false, "Нэвтрэх нэр хамгийн багадаа 3 тэмдэгттэй байх ёстой.");
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            return (false, "И-мэйл хаяг буруу байна.");
        }

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return (false, "Гар утасны дугаар хоосон байж болохгүй.");
        }

        var usernameExists = await _dbContext.Users
            .AnyAsync(item => item.Id != user.Id && item.Username == username, cancellationToken);
        if (usernameExists)
        {
            return (false, "Энэ нэвтрэх нэр бүртгэлтэй байна.");
        }

        var emailExists = await _dbContext.Users
            .AnyAsync(item => item.Id != user.Id && item.Email == email, cancellationToken);
        if (emailExists)
        {
            return (false, "Энэ и-мэйл хаяг бүртгэлтэй байна.");
        }

        var oldValue = new
        {
            user.Username,
            user.Email,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.EmergencyPhoneNumber
        };

        user.Username = username;
        user.Email = email;
        user.FirstName = firstName;
        user.LastName = lastName;
        user.PhoneNumber = phoneNumber;
        user.EmergencyPhoneNumber = emergencyPhone;
        user.UpdatedAt = MongoliaClock.Now;

        AddAuditLog(
            adminUserId,
            "ADMIN_USER_PROFILE_UPDATED",
            "users",
            user.Id,
            oldValue,
            new
            {
                user.Username,
                user.Email,
                user.FirstName,
                user.LastName,
                user.PhoneNumber,
                user.EmergencyPhoneNumber
            },
            $"Admin updated user {user.Username} profile settings.");

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, "Хэрэглэгчийн мэдээлэл амжилттай шинэчлэгдлээ.");
    }

    public async Task<(bool Success, string? ErrorMessage)> SetUserPasswordResetRequiredAsync(
        long adminUserId,
        long userId,
        bool isRequired,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return (false, "Хэрэглэгч олдсонгүй.");
        }

        if (user.PasswordResetRequired == isRequired)
        {
            return (true, isRequired
                ? "Нууц үг шинэчлэх шаардлага аль хэдийн тавигдсан байна."
                : "Нууц үг шинэчлэх шаардлага аль хэдийн цуцлагдсан байна.");
        }

        var oldValue = new { user.PasswordResetRequired };
        user.PasswordResetRequired = isRequired;
        user.UpdatedAt = MongoliaClock.Now;

        AddAuditLog(
            adminUserId,
            "ADMIN_USER_PASSWORD_RESET_REQUIRED_UPDATED",
            "users",
            user.Id,
            oldValue,
            new { user.PasswordResetRequired },
            isRequired
                ? $"Admin required user {user.Username} to reset password."
                : $"Admin cancelled password reset requirement for user {user.Username}.");

        _dbContext.Notifications.Add(new Notification
        {
            UserId = user.Id,
            NotificationType = "SECURITY_REVIEW_UPDATE",
            Title = "Нууц үг шинэчлэх шаардлага",
            Message = isRequired
                ? "Phoebe Bank таны бүртгэлд нууц үг шинэчлэх шаардлага тавилаа. Дараагийн нэвтрэлтээр шинэ нууц үгээ тохируулна уу."
                : "Phoebe Bank таны бүртгэл дээрх нууц үг шинэчлэх шаардлагыг цуцаллаа.",
            IsRead = false,
            CreatedAt = user.UpdatedAt
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, isRequired
            ? "Хэрэглэгч дараагийн нэвтрэлтээр нууц үгээ шинэчлэх шаардлагатай боллоо."
            : "Нууц үг шинэчлэх шаардлага цуцлагдлаа.");
    }

    public async Task<(bool Success, string? ErrorMessage)> UnlockUserAsync(
        long adminUserId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return (false, "Хэрэглэгч олдсонгүй.");
        }

        var oldValue = new
        {
            user.FailedLoginCount,
            user.LockedUntil,
            user.LockedUntilUtc,
            user.LockedUntilServerTick
        };

        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.LockedUntilUtc = null;
        user.LockedUntilServerTick = null;
        user.LastFailedLoginAt = null;
        user.LastFailedLoginAtUtc = null;
        user.LastFailedLoginServerTick = null;
        user.UpdatedAt = MongoliaClock.Now;

        AddAuditLog(
            adminUserId,
            "ADMIN_USER_UNLOCKED",
            "users",
            user.Id,
            oldValue,
            new
            {
                user.FailedLoginCount,
                user.LockedUntil,
                user.LockedUntilUtc,
                user.LockedUntilServerTick
            },
            $"Admin unlocked user {user.Username}.");

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, "Хэрэглэгчийн түгжээ амжилттай тайлагдлаа.");
    }

    public async Task<AdminPagedResultDto<AdminAccountDto>> GetAccountsAsync(
        AdminAccountFilterDto? filter = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Account> query = _dbContext.Accounts
            .AsNoTracking()
            .Include(account => account.User);
        var normalizedSearch = NormalizeSearch(filter?.Search);
        if (normalizedSearch is not null)
        {
            query = query.Where(account =>
                account.AccountNumber.Contains(normalizedSearch) ||
                account.Currency.Contains(normalizedSearch) ||
                account.AccountType.Contains(normalizedSearch) ||
                account.User.Username.Contains(normalizedSearch) ||
                ((account.User.FirstName ?? "") + " " + (account.User.LastName ?? "")).Contains(normalizedSearch));
        }

        var username = NormalizeSearch(filter?.Username);
        if (username is not null)
        {
            query = query.Where(account => account.User.Username.Contains(username));
        }

        var ownerName = NormalizeSearch(filter?.OwnerName);
        if (ownerName is not null)
        {
            query = query.Where(account =>
                ((account.User.FirstName ?? "") + " " + (account.User.LastName ?? "")).Contains(ownerName) ||
                ((account.User.LastName ?? "") + " " + (account.User.FirstName ?? "")).Contains(ownerName));
        }

        var accountNumber = NormalizeSearch(filter?.AccountNumber);
        if (accountNumber is not null)
        {
            query = query.Where(account => account.AccountNumber.Contains(accountNumber));
        }

        var currency = NormalizeSearch(filter?.Currency)?.ToUpperInvariant();
        if (currency is not null)
        {
            query = query.Where(account => account.Currency == currency);
        }

        var accountType = NormalizeSearch(filter?.AccountType)?.ToUpperInvariant();
        if (accountType is not null)
        {
            query = query.Where(account => account.AccountType == accountType);
        }

        var status = NormalizeSearch(filter?.Status)?.ToUpperInvariant();
        if (status is "ACTIVE")
        {
            query = query.Where(account => account.IsActive);
        }
        else if (status is "INACTIVE")
        {
            query = query.Where(account => !account.IsActive);
        }

        if (filter?.MinBalance is decimal minBalance)
        {
            query = query.Where(account => account.Balance >= minBalance);
        }

        if (filter?.MaxBalance is decimal maxBalance)
        {
            query = query.Where(account => account.Balance <= maxBalance);
        }

        if (filter?.MinDailyLimitMnt is decimal minLimit)
        {
            query = query.Where(account => account.DailyTransactionLimitMnt >= minLimit);
        }

        if (filter?.MaxDailyLimitMnt is decimal maxLimit)
        {
            query = query.Where(account => account.DailyTransactionLimitMnt <= maxLimit);
        }

        if (filter?.CreatedFrom is DateOnly createdFrom)
        {
            var start = createdFrom.ToDateTime(TimeOnly.MinValue);
            query = query.Where(account => account.CreatedAt >= start);
        }

        if (filter?.CreatedTo is DateOnly createdTo)
        {
            var endExclusive = createdTo.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(account => account.CreatedAt < endExclusive);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var pageInfo = NormalizePage(page, pageSize, totalItems);
        var accounts = await query
            .OrderByDescending(account => account.CreatedAt)
            .ThenByDescending(account => account.Id)
            .Skip((pageInfo.Page - 1) * pageInfo.PageSize)
            .Take(pageInfo.PageSize)
            .Select(account => new AdminAccountDto
            {
                Id = account.Id,
                UserId = account.UserId,
                Username = account.User.Username,
                FullName = account.User.LastName == null || account.User.LastName == ""
                    ? (account.User.FirstName ?? "")
                    : account.User.FirstName == null || account.User.FirstName == ""
                        ? account.User.LastName + "-ийн"
                        : account.User.LastName + "-ийн " + account.User.FirstName,
                AccountNumber = account.AccountNumber,
                AccountType = account.AccountType,
                Currency = account.Currency,
                Balance = account.Balance,
                DailyTransactionLimitMnt = account.DailyTransactionLimitMnt,
                IsActive = account.IsActive,
                IsAdminLocked = account.IsAdminLocked,
                AdminLockedAt = account.AdminLockedAt,
                CreatedAt = account.CreatedAt
            })
            .ToListAsync(cancellationToken);

        foreach (var account in accounts.Where(account => string.IsNullOrWhiteSpace(account.FullName)))
        {
            account.FullName = null;
        }

        return new AdminPagedResultDto<AdminAccountDto>
        {
            Items = accounts,
            Page = pageInfo.Page,
            PageSize = pageInfo.PageSize,
            TotalItems = totalItems
        };
    }

    public async Task<AdminAccountLimitDetailsDto?> GetAccountLimitDetailsAsync(
        long accountId,
        CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.Id == accountId)
            .Select(account => new AdminAccountLimitDetailsDto
            {
                AccountId = account.Id,
                AccountNumber = account.AccountNumber,
                Currency = account.Currency,
                OwnerName = account.User.LastName == null || account.User.LastName == ""
                    ? (account.User.FirstName ?? "")
                    : account.User.FirstName == null || account.User.FirstName == ""
                        ? account.User.LastName + "-ийн"
                        : account.User.LastName + "-ийн " + account.User.FirstName,
                CurrentDailyLimitMnt = account.DailyTransactionLimitMnt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (account is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(account.OwnerName))
        {
            account.OwnerName = null;
        }

        account.Histories = await _dbContext.AccountTransactionLimitHistories
            .AsNoTracking()
            .Where(history => history.AccountId == accountId)
            .OrderByDescending(history => history.CreatedAt)
            .ThenByDescending(history => history.Id)
            .Take(20)
            .Select(history => new AdminAccountLimitHistoryDto
            {
                Id = history.Id,
                OldLimitAmount = history.OldLimitAmount,
                NewLimitAmount = history.NewLimitAmount,
                ChangedByUsername = history.ChangedByUser == null ? null : history.ChangedByUser.Username,
                Reason = history.Reason,
                CreatedAt = history.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return account;
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateAccountTransactionLimitAsync(
        long adminUserId,
        UpdateAccountTransactionLimitDto dto,
        CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(account => account.Id == dto.AccountId, cancellationToken);

        if (account is null)
        {
            return (false, "Данс олдсонгүй.");
        }

        if (dto.DailyLimitMnt <= 0)
        {
            return (false, "Өдрийн лимитийн дүн 0-ээс их байх ёстой.");
        }

        var nextLimit = decimal.Round(dto.DailyLimitMnt, 2, MidpointRounding.AwayFromZero);
        var oldLimit = account.DailyTransactionLimitMnt;
        if (oldLimit == nextLimit)
        {
            return (true, "Өдрийн гүйлгээний лимит өөрчлөгдөөгүй байна.");
        }

        var now = MongoliaClock.Now;
        var reason = string.IsNullOrWhiteSpace(dto.Reason)
            ? null
            : dto.Reason.Trim();

        await using var dbTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        account.DailyTransactionLimitMnt = nextLimit;
        account.UpdatedAt = now;

        _dbContext.AccountTransactionLimitHistories.Add(new AccountTransactionLimitHistory
        {
            AccountId = account.Id,
            OldLimitAmount = oldLimit,
            NewLimitAmount = nextLimit,
            ChangedByUserId = adminUserId,
            Reason = reason,
            CreatedAt = now
        });

        AddAuditLog(
            adminUserId,
            "ACCOUNT_TRANSACTION_LIMIT_UPDATED",
            "accounts",
            account.Id,
            new { DailyTransactionLimitMnt = oldLimit },
            new { DailyTransactionLimitMnt = nextLimit },
            $"Account {account.AccountNumber} daily transaction limit updated.");

        await _dbContext.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return (true, "Өдрийн гүйлгээний лимит амжилттай шинэчлэгдлээ.");
    }

    public async Task<AdminPagedResultDto<AdminTransactionDto>> GetTransactionsAsync(
        AdminTransactionFilterDto? filter = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Transaction> query = _dbContext.Transactions
            .AsNoTracking()
            .Include(transaction => transaction.FromAccount)
            .Include(transaction => transaction.ToAccount)
            .Include(transaction => transaction.TransactionDetectionLogs);
        var normalizedSearch = NormalizeSearch(filter?.Search);
        if (normalizedSearch is not null)
        {
            query = query.Where(transaction =>
                transaction.Id.ToString().Contains(normalizedSearch) ||
                transaction.FromAccount.AccountNumber.Contains(normalizedSearch) ||
                transaction.ToAccount.AccountNumber.Contains(normalizedSearch) ||
                transaction.Status.Contains(normalizedSearch) ||
                (transaction.Description ?? "").Contains(normalizedSearch));
        }

        var accountNumber = NormalizeSearch(filter?.AccountNumber);
        if (accountNumber is not null)
        {
            query = query.Where(transaction =>
                transaction.FromAccount.AccountNumber.Contains(accountNumber) ||
                transaction.ToAccount.AccountNumber.Contains(accountNumber));
        }

        var username = NormalizeSearch(filter?.Username);
        if (username is not null)
        {
            query = query.Where(transaction =>
                transaction.FromAccount.User.Username.Contains(username) ||
                transaction.ToAccount.User.Username.Contains(username) ||
                ((transaction.FromAccount.User.FirstName ?? "") + " " + (transaction.FromAccount.User.LastName ?? "")).Contains(username) ||
                ((transaction.ToAccount.User.FirstName ?? "") + " " + (transaction.ToAccount.User.LastName ?? "")).Contains(username));
        }

        var currency = NormalizeSearch(filter?.Currency)?.ToUpperInvariant();
        if (currency is not null)
        {
            query = query.Where(transaction => transaction.SourceCurrency == currency || transaction.TargetCurrency == currency);
        }

        var status = NormalizeSearch(filter?.Status)?.ToUpperInvariant();
        if (status is not null)
        {
            query = query.Where(transaction => transaction.Status == status);
        }

        var suspiciousStatus = NormalizeSearch(filter?.SuspiciousStatus)?.ToUpperInvariant();
        if (suspiciousStatus is "YES")
        {
            query = query.Where(transaction => transaction.IsSuspicious);
        }
        else if (suspiciousStatus is "NO")
        {
            query = query.Where(transaction => !transaction.IsSuspicious);
        }

        var detectionStatus = NormalizeSearch(filter?.DetectionStatus)?.ToUpperInvariant();
        if (detectionStatus is "NONE")
        {
            query = query.Where(transaction => !transaction.TransactionDetectionLogs.Any());
        }
        else if (detectionStatus is not null)
        {
            query = query.Where(transaction => transaction.TransactionDetectionLogs
                .OrderByDescending(log => log.CreatedAt)
                .Select(log => log.ServiceStatus)
                .FirstOrDefault() == detectionStatus);
        }

        var minAmount = filter?.MinAmount;
        var maxAmount = filter?.MaxAmount;
        if (minAmount is not null || maxAmount is not null)
        {
            if (currency is not null)
            {
                query = query.Where(transaction =>
                    (transaction.SourceCurrency == currency &&
                     (minAmount == null || transaction.Amount >= minAmount.Value) &&
                     (maxAmount == null || transaction.Amount <= maxAmount.Value)) ||
                    (transaction.TargetCurrency == currency &&
                     (minAmount == null || transaction.CreditedAmount >= minAmount.Value) &&
                     (maxAmount == null || transaction.CreditedAmount <= maxAmount.Value)));
            }
            else
            {
                query = query.Where(transaction =>
                    (minAmount == null || transaction.Amount >= minAmount.Value) &&
                    (maxAmount == null || transaction.Amount <= maxAmount.Value));
            }
        }

        if (filter?.StartDate is DateOnly startDate)
        {
            var start = startDate.ToDateTime(TimeOnly.MinValue);
            query = query.Where(transaction => transaction.CreatedAt >= start);
        }

        if (filter?.EndDate is DateOnly endDate)
        {
            var endExclusive = endDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(transaction => transaction.CreatedAt < endExclusive);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var pageInfo = NormalizePage(page, pageSize, totalItems);
        var transactions = await query
            .OrderByDescending(transaction => transaction.CreatedAt)
            .ThenByDescending(transaction => transaction.Id)
            .Skip((pageInfo.Page - 1) * pageInfo.PageSize)
            .Take(pageInfo.PageSize)
            .Select(transaction => new AdminTransactionDto
            {
                Id = transaction.Id,
                FromAccountNumber = transaction.FromAccount.AccountNumber,
                ToAccountNumber = transaction.ToAccount.AccountNumber,
                Amount = transaction.Amount,
                SourceCurrency = transaction.SourceCurrency,
                CreditedAmount = transaction.CreditedAmount,
                TargetCurrency = transaction.TargetCurrency,
                Status = transaction.Status,
                Description = transaction.Description,
                IsSuspicious = transaction.IsSuspicious,
                DetectionStatus = transaction.TransactionDetectionLogs
                    .OrderByDescending(log => log.CreatedAt)
                    .Select(log => log.ServiceStatus)
                    .FirstOrDefault(),
                DetectionRiskScore = transaction.TransactionDetectionLogs
                    .OrderByDescending(log => log.CreatedAt)
                    .Select(log => log.RiskScore)
                    .FirstOrDefault(),
                DetectionReason = transaction.TransactionDetectionLogs
                    .OrderByDescending(log => log.CreatedAt)
                    .Select(log => log.Reason)
                    .FirstOrDefault(),
                DetectionTriggeredRules = transaction.TransactionDetectionLogs
                    .OrderByDescending(log => log.CreatedAt)
                    .Select(log => log.TriggeredRules)
                    .FirstOrDefault(),
                DetectionLoggedAt = transaction.TransactionDetectionLogs
                    .OrderByDescending(log => log.CreatedAt)
                    .Select(log => (DateTime?)log.CreatedAt)
                    .FirstOrDefault(),
                CreatedAt = transaction.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new AdminPagedResultDto<AdminTransactionDto>
        {
            Items = transactions,
            Page = pageInfo.Page,
            PageSize = pageInfo.PageSize,
            TotalItems = totalItems
        };
    }

    public async Task<AdminAiDetectionPageDto> GetAiDetectionPageAsync(
        string? search = null,
        string? username = null,
        string? currency = null,
        decimal? minRiskScore = null,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        long? chatTransactionId = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var normalizedPageSize = pageSize is 100 ? 100 : 50;
        var query = BuildAiDetectionTransactionQuery(search, username, currency, minRiskScore, startDate, endDate);
        var totalItems = await query.CountAsync(cancellationToken);
        var pageInfo = NormalizePage(page, normalizedPageSize, totalItems);
        var items = await query
            .OrderByDescending(transaction => transaction.CreatedAt)
            .ThenByDescending(transaction => transaction.Id)
            .Skip((pageInfo.Page - 1) * pageInfo.PageSize)
            .Take(pageInfo.PageSize)
            .Select(transaction => new AdminAiDetectionTransactionDto
            {
                Id = transaction.Id,
                CreatedAt = transaction.CreatedAt,
                FromAccountNumber = transaction.FromAccount.AccountNumber,
                FromUserName = transaction.FromAccount.User.LastName == null || transaction.FromAccount.User.LastName == ""
                    ? (transaction.FromAccount.User.FirstName ?? "")
                    : transaction.FromAccount.User.FirstName == null || transaction.FromAccount.User.FirstName == ""
                        ? transaction.FromAccount.User.LastName + "-ийн"
                        : transaction.FromAccount.User.LastName + "-ийн " + transaction.FromAccount.User.FirstName,
                ToAccountNumber = transaction.ToAccount.AccountNumber,
                ToUserName = transaction.ToAccount.User.LastName == null || transaction.ToAccount.User.LastName == ""
                    ? (transaction.ToAccount.User.FirstName ?? "")
                    : transaction.ToAccount.User.FirstName == null || transaction.ToAccount.User.FirstName == ""
                        ? transaction.ToAccount.User.LastName + "-ийн"
                        : transaction.ToAccount.User.LastName + "-ийн " + transaction.ToAccount.User.FirstName,
                Amount = transaction.Amount,
                SourceCurrency = transaction.SourceCurrency,
                CreditedAmount = transaction.CreditedAmount,
                TargetCurrency = transaction.TargetCurrency,
                Status = transaction.Status,
                Description = transaction.Description,
                IsSuspicious = transaction.IsSuspicious,
                RuleRiskScore = transaction.TransactionDetectionLogs
                    .OrderByDescending(log => log.CreatedAt)
                    .Select(log => log.RiskScore)
                    .FirstOrDefault(),
                RuleReason = transaction.TransactionDetectionLogs
                    .OrderByDescending(log => log.CreatedAt)
                    .Select(log => log.Reason)
                    .FirstOrDefault(),
                RuleCheckedAt = transaction.TransactionDetectionLogs
                    .OrderByDescending(log => log.CreatedAt)
                    .Select(log => (DateTime?)log.CreatedAt)
                    .FirstOrDefault(),
                LatestAiIsSuspicious = transaction.AiTransactionAnalysisLogs
                    .OrderByDescending(log => log.CreatedAt)
                    .Select(log => log.IsSuspicious)
                    .FirstOrDefault(),
                LatestAiRiskScore = transaction.AiTransactionAnalysisLogs
                    .OrderByDescending(log => log.CreatedAt)
                    .Select(log => log.RiskScore)
                    .FirstOrDefault(),
                LatestAiExplanation = transaction.AiTransactionAnalysisLogs
                    .OrderByDescending(log => log.CreatedAt)
                    .Select(log => log.Explanation)
                    .FirstOrDefault(),
                LatestAiAnalyzedAt = transaction.AiTransactionAnalysisLogs
                    .OrderByDescending(log => log.CreatedAt)
                    .Select(log => (DateTime?)log.CreatedAt)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var pageModel = new AdminAiDetectionPageDto
        {
            Transactions = new AdminPagedResultDto<AdminAiDetectionTransactionDto>
            {
                Items = items,
                Page = pageInfo.Page,
                PageSize = pageInfo.PageSize,
                TotalItems = totalItems
            }
        };

        if (chatTransactionId is not null)
        {
            pageModel.ChatTransaction = items.FirstOrDefault(item => item.Id == chatTransactionId.Value)
                ?? await BuildAiDetectionTransactionQuery(null, null, null, null, null, null)
                    .Where(transaction => transaction.Id == chatTransactionId.Value)
                    .Select(transaction => new AdminAiDetectionTransactionDto
                    {
                        Id = transaction.Id,
                        CreatedAt = transaction.CreatedAt,
                        FromAccountNumber = transaction.FromAccount.AccountNumber,
                        FromUserName = transaction.FromAccount.User.LastName == null || transaction.FromAccount.User.LastName == ""
                            ? (transaction.FromAccount.User.FirstName ?? "")
                            : transaction.FromAccount.User.FirstName == null || transaction.FromAccount.User.FirstName == ""
                                ? transaction.FromAccount.User.LastName + "-ийн"
                                : transaction.FromAccount.User.LastName + "-ийн " + transaction.FromAccount.User.FirstName,
                        ToAccountNumber = transaction.ToAccount.AccountNumber,
                        ToUserName = transaction.ToAccount.User.LastName == null || transaction.ToAccount.User.LastName == ""
                            ? (transaction.ToAccount.User.FirstName ?? "")
                            : transaction.ToAccount.User.FirstName == null || transaction.ToAccount.User.FirstName == ""
                                ? transaction.ToAccount.User.LastName + "-ийн"
                                : transaction.ToAccount.User.LastName + "-ийн " + transaction.ToAccount.User.FirstName,
                        Amount = transaction.Amount,
                        SourceCurrency = transaction.SourceCurrency,
                        CreditedAmount = transaction.CreditedAmount,
                        TargetCurrency = transaction.TargetCurrency,
                        Status = transaction.Status,
                        Description = transaction.Description,
                        IsSuspicious = transaction.IsSuspicious,
                        RuleRiskScore = transaction.TransactionDetectionLogs.OrderByDescending(log => log.CreatedAt).Select(log => log.RiskScore).FirstOrDefault(),
                        RuleReason = transaction.TransactionDetectionLogs.OrderByDescending(log => log.CreatedAt).Select(log => log.Reason).FirstOrDefault(),
                        LatestAiExplanation = transaction.AiTransactionAnalysisLogs.OrderByDescending(log => log.CreatedAt).Select(log => log.Explanation).FirstOrDefault(),
                        LatestAiAnalyzedAt = transaction.AiTransactionAnalysisLogs.OrderByDescending(log => log.CreatedAt).Select(log => (DateTime?)log.CreatedAt).FirstOrDefault()
                    })
                    .FirstOrDefaultAsync(cancellationToken);

            pageModel.ChatMessages = await _dbContext.ChatLogs
                .AsNoTracking()
                .Where(log => log.RelatedTransactionId == chatTransactionId.Value && log.IntentType == "ADMIN_REVIEW_HELP")
                .OrderByDescending(log => log.CreatedAt)
                .Take(10)
                .OrderBy(log => log.CreatedAt)
                .Select(log => new AdminAiDetectionChatMessageDto
                {
                    Question = log.UserMessage,
                    Answer = log.BotResponse,
                    CreatedAt = log.CreatedAt
                })
                .ToListAsync(cancellationToken);
        }

        foreach (var item in pageModel.Transactions.Items.Where(item => string.IsNullOrWhiteSpace(item.FromUserName)))
        {
            item.FromUserName = "-";
        }

        foreach (var item in pageModel.Transactions.Items.Where(item => string.IsNullOrWhiteSpace(item.ToUserName)))
        {
            item.ToUserName = "-";
        }

        return pageModel;
    }

    public async Task<(bool Success, string? ErrorMessage, int AnalyzedCount)> AnalyzeTransactionsWithAiAsync(
        long adminUserId,
        IReadOnlyList<long> transactionIds,
        string? modelName = null,
        CancellationToken cancellationToken = default)
    {
        var ids = transactionIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
        {
            return (false, "AI шинжилгээ хийх гүйлгээ сонгоно уу.", 0);
        }

        if (ids.Count > 1)
        {
            return (false, "AI Detection-ийг нэг удаад зөвхөн нэг гүйлгээнд ажиллуулна.", 0);
        }

        var analyzedCount = 0;
        var errors = new List<string>();

        foreach (var transactionId in ids)
        {
            var context = await BuildGeminiAnalysisContextAsync(transactionId, cancellationToken);
            if (context is null)
            {
                errors.Add($"#{transactionId}: гүйлгээ олдсонгүй.");
                continue;
            }

            var result = await _geminiAnalysisService.AnalyzeTransactionAsync(context, modelName, cancellationToken);
            if (!result.Success || result.Result is null)
            {
                errors.Add($"#{transactionId}: {result.ErrorMessage ?? "AI шинжилгээ амжилтгүй."}");
                continue;
            }

            _dbContext.AiTransactionAnalysisLogs.Add(new AiTransactionAnalysisLog
            {
                TransactionId = transactionId,
                AnalyzedBy = adminUserId,
                ModelName = result.Result.ModelName,
                IsSuspicious = result.Result.IsSuspicious,
                RiskScore = result.Result.RiskScore,
                Explanation = result.Result.Explanation,
                RecommendedAction = result.Result.RecommendedAction,
                SourceContextJson = JsonSerializer.Serialize(context),
                CreatedAt = MongoliaClock.Now
            });

            AddAuditLog(
                adminUserId,
                "AI_TRANSACTION_ANALYSIS",
                "transactions",
                transactionId,
                new { },
                new
                {
                    result.Result.IsSuspicious,
                    result.Result.RiskScore,
                    result.Result.ModelName
                },
                "Admin generated Gemini analysis for transaction.");

            analyzedCount++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (analyzedCount == 0)
        {
            return (false, string.Join(" ", errors.Take(3)), analyzedCount);
        }

        var message = errors.Count == 0
            ? $"{analyzedCount} гүйлгээнд AI шинжилгээ хийлээ."
            : $"{analyzedCount} гүйлгээнд AI шинжилгээ хийлээ. Зарим гүйлгээ амжилтгүй: {string.Join(" ", errors.Take(3))}";

        return (true, message, analyzedCount);
    }

    public async Task<(bool Success, string? ErrorMessage)> AskAiDetectionQuestionAsync(
        long adminUserId,
        long transactionId,
        string question,
        string? modelName = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return (false, "Асуулт хоосон байна.");
        }

        var context = await BuildGeminiAnalysisContextAsync(transactionId, cancellationToken);
        if (context is null)
        {
            return (false, "Гүйлгээ олдсонгүй.");
        }

        var latestAnalysis = await _dbContext.AiTransactionAnalysisLogs
            .AsNoTracking()
            .Where(log => log.TransactionId == transactionId)
            .OrderByDescending(log => log.CreatedAt)
            .Select(log => log.Explanation)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(latestAnalysis))
        {
            return (false, "Эхлээд энэ гүйлгээнд AI Detection ажиллуулна уу.");
        }

        var result = await _geminiAnalysisService.AskTransactionAnalysisQuestionAsync(
            context,
            latestAnalysis,
            question,
            modelName,
            cancellationToken);

        if (!result.Success || string.IsNullOrWhiteSpace(result.Answer))
        {
            return (false, result.ErrorMessage ?? "AI chat хариу авч чадсангүй.");
        }

        _dbContext.ChatLogs.Add(new ChatLog
        {
            UserId = adminUserId,
            SessionId = Guid.NewGuid(),
            IntentType = "ADMIN_REVIEW_HELP",
            UserMessage = question.Trim(),
            BotResponse = result.Answer,
            UsedContextType = "TRANSACTION_AI_ANALYSIS",
            RelatedTransactionId = transactionId,
            CreatedAt = MongoliaClock.Now
        });

        AddAuditLog(
            adminUserId,
            "AI_TRANSACTION_CHAT",
            "transactions",
            transactionId,
            new { },
            new { Question = question.Trim() },
            "Admin asked Gemini follow-up question about transaction.");

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, "AI chat хариу бэлэн боллоо.");
    }

    public async Task<AdminPagedResultDto<AdminAuditLogDto>> GetAuditLogsAsync(
        AdminAuditLogFilterDto? filter = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var normalizedSource = NormalizeAuditSource(filter?.Source);
        var normalizedSearch = NormalizeSearch(filter?.Search);
        var start = filter?.StartDate?.ToDateTime(TimeOnly.MinValue);
        var endExclusive = filter?.EndDate?.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var normalizedPageSize = Math.Clamp(pageSize, 5, 100);
        var requestedPage = Math.Max(1, page);

        var auditQuery = BuildAuditLogQuery(normalizedSource, normalizedSearch, start, endExclusive);
        var securityQuery = BuildSecurityEventQuery(normalizedSource, normalizedSearch, start, endExclusive);
        var rateQuery = BuildCurrencyRateAuditQuery(normalizedSource, normalizedSearch, start, endExclusive);
        var detectionQuery = BuildFraudDetectionQuery(normalizedSource, normalizedSearch, start, endExclusive);

        var totalItems =
            await auditQuery.CountAsync(cancellationToken) +
            await securityQuery.CountAsync(cancellationToken) +
            await rateQuery.CountAsync(cancellationToken) +
            await detectionQuery.CountAsync(cancellationToken);

        var pageInfo = NormalizePage(requestedPage, normalizedPageSize, totalItems);
        var takePerSource = pageInfo.Page * pageInfo.PageSize;
        var records = new List<AdminAuditLogDto>();

        records.AddRange((await auditQuery
                .OrderByDescending(log => log.CreatedAt)
                .ThenByDescending(log => log.Id)
                .Take(takePerSource)
                .ToListAsync(cancellationToken))
            .Select(MapAuditLog));

        records.AddRange((await securityQuery
                .OrderByDescending(log => log.CreatedAt)
                .ThenByDescending(log => log.Id)
                .Take(takePerSource)
                .ToListAsync(cancellationToken))
            .Select(MapSecurityEventLog));

        records.AddRange((await rateQuery
                .OrderByDescending(log => log.ChangedAt)
                .ThenByDescending(log => log.Id)
                .Take(takePerSource)
                .ToListAsync(cancellationToken))
            .Select(MapCurrencyRateAudit));

        records.AddRange((await detectionQuery
                .OrderByDescending(log => log.CreatedAt)
                .ThenByDescending(log => log.Id)
                .Take(takePerSource)
                .ToListAsync(cancellationToken))
            .Select(MapFraudDetectionLog));

        var items = records
            .OrderByDescending(log => log.OccurredAt)
            .ThenByDescending(log => log.SourceId)
            .Skip((pageInfo.Page - 1) * pageInfo.PageSize)
            .Take(pageInfo.PageSize)
            .ToList();

        return new AdminPagedResultDto<AdminAuditLogDto>
        {
            Items = items,
            Page = pageInfo.Page,
            PageSize = pageInfo.PageSize,
            TotalItems = totalItems
        };
    }

    public async Task<AdminFxIncomeReportDto> GetFxIncomeReportAsync(
        AdminFxIncomeFilterDto? filter = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var today = MongoliaClock.Today;
        var normalizedStartDate = filter?.StartDate ?? new DateOnly(today.Year, today.Month, 1);
        var normalizedEndDate = filter?.EndDate ?? today;

        if (normalizedEndDate < normalizedStartDate)
        {
            (normalizedStartDate, normalizedEndDate) = (normalizedEndDate, normalizedStartDate);
        }

        var startDateTime = normalizedStartDate.ToDateTime(TimeOnly.MinValue);
        var endExclusive = normalizedEndDate.AddDays(1).ToDateTime(TimeOnly.MinValue);

        IQueryable<FxIncomeLog> query = _dbContext.FxIncomeLogs
            .AsNoTracking()
            .Include(log => log.Transaction)
                .ThenInclude(transaction => transaction.FromAccount)
            .Include(log => log.Transaction)
                .ThenInclude(transaction => transaction.ToAccount)
            .Where(log => log.CreatedAt >= startDateTime && log.CreatedAt < endExclusive);

        var normalizedSearch = NormalizeSearch(filter?.Search);
        if (normalizedSearch is not null)
        {
            if (long.TryParse(normalizedSearch, out var transactionId))
            {
                query = query.Where(log => log.TransactionId == transactionId);
            }
            else
            {
                query = query.Where(log =>
                    log.FromCurrency.Contains(normalizedSearch) ||
                    log.ToCurrency.Contains(normalizedSearch) ||
                    log.IncomeType.Contains(normalizedSearch) ||
                    log.Source.Contains(normalizedSearch) ||
                    log.Transaction.FromAccount.AccountNumber.Contains(normalizedSearch) ||
                    log.Transaction.ToAccount.AccountNumber.Contains(normalizedSearch) ||
                    (log.Transaction.Description ?? "").Contains(normalizedSearch));
            }
        }

        var accountNumber = NormalizeSearch(filter?.AccountNumber);
        if (accountNumber is not null)
        {
            query = query.Where(log =>
                log.Transaction.FromAccount.AccountNumber.Contains(accountNumber) ||
                log.Transaction.ToAccount.AccountNumber.Contains(accountNumber));
        }

        var fromCurrency = NormalizeSearch(filter?.FromCurrency)?.ToUpperInvariant();
        if (fromCurrency is not null)
        {
            query = query.Where(log => log.FromCurrency == fromCurrency);
        }

        var toCurrency = NormalizeSearch(filter?.ToCurrency)?.ToUpperInvariant();
        if (toCurrency is not null)
        {
            query = query.Where(log => log.ToCurrency == toCurrency);
        }

        var incomeType = NormalizeSearch(filter?.IncomeType)?.ToUpperInvariant();
        if (incomeType is not null)
        {
            query = query.Where(log => log.IncomeType == incomeType);
        }

        if (filter?.MinIncomeMnt is decimal minIncome)
        {
            query = query.Where(log => log.IncomeAmountMnt >= minIncome);
        }

        if (filter?.MaxIncomeMnt is decimal maxIncome)
        {
            query = query.Where(log => log.IncomeAmountMnt <= maxIncome);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalIncome = await query.SumAsync(log => (decimal?)log.IncomeAmountMnt, cancellationToken) ?? 0m;
        var buyIncome = await query
            .Where(log => log.IncomeType == "FX_BUY_SPREAD")
            .SumAsync(log => (decimal?)log.IncomeAmountMnt, cancellationToken) ?? 0m;
        var sellIncome = await query
            .Where(log => log.IncomeType == "FX_SELL_SPREAD")
            .SumAsync(log => (decimal?)log.IncomeAmountMnt, cancellationToken) ?? 0m;
        var averageSpread = await query.AverageAsync(log => (decimal?)log.SpreadMarginMntPerUsd, cancellationToken) ?? 0m;

        var pageInfo = NormalizePage(page, pageSize, totalItems);
        var logs = await query
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .Skip((pageInfo.Page - 1) * pageInfo.PageSize)
            .Take(pageInfo.PageSize)
            .Select(log => new AdminFxIncomeDto
            {
                Id = log.Id,
                TransactionId = log.TransactionId,
                FromCurrency = log.FromCurrency,
                ToCurrency = log.ToCurrency,
                SourceAmount = log.SourceAmount,
                CreditedAmount = log.CreditedAmount,
                OfficialRateMntPerUsd = log.OfficialRateMntPerUsd,
                CustomerRateMntPerUsd = log.CustomerRateMntPerUsd,
                SpreadMarginMntPerUsd = log.SpreadMarginMntPerUsd,
                IncomeAmountMnt = log.IncomeAmountMnt,
                IncomeType = log.IncomeType,
                Source = log.Source,
                RateDate = log.RateDate,
                CreatedAt = log.CreatedAt,
                FromAccountNumber = log.Transaction.FromAccount.AccountNumber,
                ToAccountNumber = log.Transaction.ToAccount.AccountNumber,
                Description = log.Transaction.Description
            })
            .ToListAsync(cancellationToken);

        return new AdminFxIncomeReportDto
        {
            Summary = new AdminFxIncomeSummaryDto
            {
                TotalIncomeMnt = totalIncome,
                BuyIncomeMnt = buyIncome,
                SellIncomeMnt = sellIncome,
                AverageSpreadMarginMntPerUsd = averageSpread,
                TotalItems = totalItems,
                StartDate = normalizedStartDate,
                EndDate = normalizedEndDate
            },
            Logs = new AdminPagedResultDto<AdminFxIncomeDto>
            {
                Items = logs,
                Page = pageInfo.Page,
                PageSize = pageInfo.PageSize,
                TotalItems = totalItems
            }
        };
    }

    public async Task<AdminSuspiciousDetectionReportDto> GetSuspiciousDetectionReportAsync(
        AdminSuspiciousDetectionReportFilterDto? filter = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var today = MongoliaClock.Today;
        var effectiveStartDate = filter?.StartDate ?? today.AddDays(-6);
        var effectiveEndDate = filter?.EndDate ?? today;

        if (effectiveEndDate < effectiveStartDate)
        {
            (effectiveStartDate, effectiveEndDate) = (effectiveEndDate, effectiveStartDate);
        }

        var start = effectiveStartDate.ToDateTime(TimeOnly.MinValue);
        var endExclusive = effectiveEndDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var normalizedSearch = NormalizeSearch(filter?.Search);
        var normalizedReviewStatus = NormalizeSearch(filter?.ReviewStatus)?.ToUpperInvariant();

        var query = BuildSuspiciousDetectionReportQuery(start, endExclusive, normalizedSearch, normalizedReviewStatus, filter?.SuspiciousOnly == true);
        var serviceStatus = NormalizeSearch(filter?.ServiceStatus)?.ToUpperInvariant();
        if (serviceStatus is not null)
        {
            query = query.Where(log => log.ServiceStatus == serviceStatus);
        }

        var ruleCode = NormalizeSearch(filter?.RuleCode)?.ToUpperInvariant();
        if (ruleCode is not null)
        {
            query = query.Where(log => (log.TriggeredRules ?? "").Contains(ruleCode));
        }

        if (filter?.MinRiskScore is decimal minRiskScore)
        {
            query = query.Where(log => log.RiskScore >= minRiskScore);
        }

        if (filter?.MaxRiskScore is decimal maxRiskScore)
        {
            query = query.Where(log => log.RiskScore <= maxRiskScore);
        }
        var totalItems = await query.CountAsync(cancellationToken);
        var pageInfo = NormalizePage(page, pageSize, totalItems);

        var allFilteredRows = await query
            .OrderBy(log => log.CreatedAt)
            .ThenBy(log => log.Id)
            .Select(log => new
            {
                log.Id,
                log.TransactionId,
                log.CreatedAt,
                log.ServiceStatus,
                log.IsSuspicious,
                log.RiskScore,
                log.Reason,
                log.TriggeredRules,
                log.Source,
                TransactionCreatedAt = log.Transaction.CreatedAt,
                log.Transaction.Amount,
                log.Transaction.SourceCurrency,
                log.Transaction.CreditedAmount,
                log.Transaction.TargetCurrency,
                FromAccountNumber = log.Transaction.FromAccount.AccountNumber,
                ToAccountNumber = log.Transaction.ToAccount.AccountNumber,
                ReviewStatus = log.Transaction.SuspiciousTransactionDetail == null
                    ? null
                    : log.Transaction.SuspiciousTransactionDetail.ReviewStatus
            })
            .ToListAsync(cancellationToken);

        var pagedRows = allFilteredRows
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .Skip((pageInfo.Page - 1) * pageInfo.PageSize)
            .Take(pageInfo.PageSize)
            .Select(log => new AdminSuspiciousDetectionLogDto
            {
                Id = log.Id,
                TransactionId = log.TransactionId,
                DetectionAt = log.CreatedAt,
                TransactionAt = log.TransactionCreatedAt,
                ServiceStatus = log.ServiceStatus,
                IsSuspicious = log.IsSuspicious,
                RiskScore = log.RiskScore,
                Reason = log.Reason ?? "-",
                TriggeredRules = ParseTriggeredRules(log.TriggeredRules),
                TriggeredRulesText = string.Join(", ", ParseTriggeredRules(log.TriggeredRules)),
                ReviewStatus = log.ReviewStatus ?? "-",
                ReviewStatusLabel = log.ReviewStatus is null ? "-" : ReviewStatusHelper.GetLabel(log.ReviewStatus),
                FromAccountNumber = log.FromAccountNumber,
                ToAccountNumber = log.ToAccountNumber,
                Amount = log.Amount,
                SourceCurrency = log.SourceCurrency,
                CreditedAmount = log.CreditedAmount,
                TargetCurrency = log.TargetCurrency,
                Source = log.Source
            })
            .ToList();

        var suspiciousRows = allFilteredRows.Where(log => log.IsSuspicious == true).ToList();
        var checkedRows = allFilteredRows.Where(log => string.Equals(log.ServiceStatus, "CHECKED", StringComparison.OrdinalIgnoreCase)).ToList();
        var ruleSummaries = allFilteredRows
            .SelectMany(log => ParseTriggeredRules(log.TriggeredRules).Select(rule => new { Rule = rule, log.RiskScore }))
            .GroupBy(item => item.Rule)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => new AdminSuspiciousDetectionRuleSummaryDto
            {
                RuleCode = group.Key,
                HitCount = group.Count(),
                AverageRiskScore = decimal.Round(group.Average(item => item.RiskScore ?? 0m), 2, MidpointRounding.AwayFromZero)
            })
            .Take(10)
            .ToList();

        var dailySuspicious = Enumerable.Range(0, (effectiveEndDate.DayNumber - effectiveStartDate.DayNumber) + 1)
            .Select(offset => effectiveStartDate.AddDays(offset))
            .Select(day => new ChartDataPoint
            {
                Label = day.ToString("MM-dd"),
                Value = allFilteredRows.Count(log => log.IsSuspicious == true && DateOnly.FromDateTime(log.CreatedAt) == day),
                Color = "#dc2626"
            })
            .ToList();

        var reviewStatusChart = suspiciousRows
            .GroupBy(log => log.ReviewStatus ?? "NO_REVIEW")
            .OrderByDescending(group => group.Count())
            .Select((group, index) => new ChartDataPoint
            {
                Label = group.Key == "NO_REVIEW" ? "Review үүсээгүй" : ReviewStatusHelper.GetLabel(group.Key),
                Value = group.Count(),
                Color = ChartColors[index % ChartColors.Length]
            })
            .ToList();

        return new AdminSuspiciousDetectionReportDto
        {
            Summary = new AdminSuspiciousDetectionSummaryDto
            {
                StartDate = effectiveStartDate,
                EndDate = effectiveEndDate,
                TotalChecks = allFilteredRows.Count,
                SuspiciousCount = suspiciousRows.Count,
                NormalCount = allFilteredRows.Count(log => log.IsSuspicious == false),
                UnavailableCount = allFilteredRows.Count(log => !string.Equals(log.ServiceStatus, "CHECKED", StringComparison.OrdinalIgnoreCase)),
                PendingReviewCount = suspiciousRows.Count(log => string.Equals(log.ReviewStatus, "PENDING", StringComparison.OrdinalIgnoreCase)),
                ReviewedCount = suspiciousRows.Count(log => log.ReviewStatus is not null && !string.Equals(log.ReviewStatus, "PENDING", StringComparison.OrdinalIgnoreCase)),
                AverageRiskScore = checkedRows.Count == 0 ? 0m : decimal.Round(checkedRows.Average(log => log.RiskScore ?? 0m), 2, MidpointRounding.AwayFromZero),
                MaxRiskScore = checkedRows.Count == 0 ? 0m : checkedRows.Max(log => log.RiskScore ?? 0m)
            },
            RuleSummaries = ruleSummaries,
            DailySuspiciousTrend = dailySuspicious,
            ReviewStatusChart = reviewStatusChart,
            Logs = new AdminPagedResultDto<AdminSuspiciousDetectionLogDto>
            {
                Items = pagedRows,
                Page = pageInfo.Page,
                PageSize = pageInfo.PageSize,
                TotalItems = totalItems
            }
        };
    }

    public async Task<List<AdminCurrencyRateSettingDto>> GetCurrencyRateSettingsAsync(CancellationToken cancellationToken = default)
    {
        var now = MongoliaClock.Now;
        var settings = await _dbContext.CurrencyRateSettings
            .Include(setting => setting.UpdatedByNavigation)
            .Include(setting => setting.CurrencyRateOverrideSchedules)
                .ThenInclude(schedule => schedule.CreatedByNavigation)
            .Include(setting => setting.CurrencyRateOverrideSchedules)
                .ThenInclude(schedule => schedule.CancelledByNavigation)
            .OrderBy(setting => setting.CurrencyCode)
            .ThenBy(setting => setting.BaseCurrency)
            .ToListAsync(cancellationToken);

        var changed = false;
        foreach (var setting in settings.Where(setting =>
                     setting.IsManualOverride &&
                     setting.ManualExpiresAt is not null &&
                     setting.ManualExpiresAt <= now))
        {
            AddCurrencyRateAudit(
                setting,
                "MANUAL_OVERRIDE_EXPIRED",
                setting.UpdatedBy,
                setting.ManualBuyRate,
                setting.ManualSellRate,
                null,
                null,
                setting.IsManualOverride,
                false,
                setting.ManualExpiresAt,
                null,
                "Manual override expired automatically.");

            setting.IsManualOverride = false;
            setting.ManualBuyRate = null;
            setting.ManualSellRate = null;
            setting.ManualExpiresAt = null;
            setting.UpdatedAt = now;
            changed = true;
        }

        if (changed)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return settings.Select(setting => MapCurrencyRateSetting(setting, now)).ToList();
    }

    public async Task<AdminFraudRuleSettingsPageDto> GetFraudRuleSettingsAsync(CancellationToken cancellationToken = default)
    {
        var detectionSetting = await _dbContext.FraudDetectionSettings
            .AsNoTracking()
            .Include(setting => setting.UpdatedByNavigation)
            .FirstOrDefaultAsync(setting => setting.Id == 1, cancellationToken);

        var rules = await _dbContext.FraudRuleSettings
            .AsNoTracking()
            .Include(setting => setting.UpdatedByNavigation)
            .OrderBy(setting => setting.RuleCode)
            .Select(setting => new AdminFraudRuleSettingDto
            {
                Id = setting.Id,
                RuleCode = setting.RuleCode,
                DisplayName = setting.DisplayName,
                Description = setting.Description,
                IsEnabled = setting.IsEnabled,
                Score = setting.Score,
                NumericThreshold = setting.NumericThreshold,
                AmountThresholdMnt = setting.AmountThresholdMnt,
                AmountThresholdUsd = setting.AmountThresholdUsd,
                UpdatedAt = setting.UpdatedAt,
                UpdatedByUsername = setting.UpdatedByNavigation == null ? null : setting.UpdatedByNavigation.Username
            })
            .ToListAsync(cancellationToken);

        return new AdminFraudRuleSettingsPageDto
        {
            SuspiciousThreshold = detectionSetting?.SuspiciousThreshold ?? 60,
            ThresholdUpdatedAt = detectionSetting?.UpdatedAt,
            ThresholdUpdatedByUsername = detectionSetting?.UpdatedByNavigation?.Username,
            Rules = rules
        };
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateFraudRuleSettingAsync(
        long adminUserId,
        UpdateFraudRuleSettingDto dto,
        CancellationToken cancellationToken = default)
    {
        var setting = await _dbContext.FraudRuleSettings
            .FirstOrDefaultAsync(setting => setting.Id == dto.Id, cancellationToken);

        if (setting is null)
        {
            return (false, "Rule тохиргоо олдсонгүй.");
        }

        if (dto.Score is < 0 or > 100)
        {
            return (false, "Rule score 0-100 хооронд байх ёстой.");
        }

        if (dto.NumericThreshold is < 0 ||
            dto.AmountThresholdMnt is < 0 ||
            dto.AmountThresholdUsd is < 0)
        {
            return (false, "Threshold утга сөрөг байж болохгүй.");
        }

        var oldValue = new
        {
            setting.IsEnabled,
            setting.Score,
            setting.NumericThreshold,
            setting.AmountThresholdMnt,
            setting.AmountThresholdUsd
        };

        setting.IsEnabled = dto.IsEnabled;
        setting.Score = dto.Score;
        setting.NumericThreshold = dto.NumericThreshold;
        setting.AmountThresholdMnt = dto.AmountThresholdMnt;
        setting.AmountThresholdUsd = dto.AmountThresholdUsd;
        setting.UpdatedBy = adminUserId;
        setting.UpdatedAt = MongoliaClock.Now;

        AddAuditLog(
            adminUserId,
            "FRAUD_RULE_SETTING_UPDATED",
            "fraud_rule_settings",
            setting.Id,
            oldValue,
            new
            {
                setting.IsEnabled,
                setting.Score,
                setting.NumericThreshold,
                setting.AmountThresholdMnt,
                setting.AmountThresholdUsd
            },
            $"{setting.RuleCode} rule тохиргоо шинэчлэгдлээ.");

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, "Rule тохиргоо шинэчлэгдлээ.");
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateFraudDetectionSettingsAsync(
        long adminUserId,
        UpdateFraudDetectionSettingsDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto.SuspiciousThreshold is < 1 or > 100)
        {
            return (false, "Сэжигтэй гэж үзэх босго 1-100 хооронд байх ёстой.");
        }

        var setting = await _dbContext.FraudDetectionSettings
            .FirstOrDefaultAsync(setting => setting.Id == 1, cancellationToken);

        if (setting is null)
        {
            setting = new FraudDetectionSetting
            {
                Id = 1,
                SuspiciousThreshold = 60,
                UpdatedAt = MongoliaClock.Now
            };
            _dbContext.FraudDetectionSettings.Add(setting);
        }

        var oldValue = new { setting.SuspiciousThreshold };
        setting.SuspiciousThreshold = dto.SuspiciousThreshold;
        setting.UpdatedBy = adminUserId;
        setting.UpdatedAt = MongoliaClock.Now;

        AddAuditLog(
            adminUserId,
            "FRAUD_DETECTION_SETTING_UPDATED",
            "fraud_detection_settings",
            setting.Id,
            oldValue,
            new { setting.SuspiciousThreshold },
            "Rule-based detection global threshold шинэчлэгдлээ.");

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, "Сэжигтэй босго шинэчлэгдлээ.");
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateCurrencyRateAlgorithmAsync(
        long adminUserId,
        UpdateCurrencyRateAlgorithmDto dto,
        CancellationToken cancellationToken = default)
    {
        var setting = await _dbContext.CurrencyRateSettings
            .Include(item => item.CurrencyRateOverrideSchedules)
            .FirstOrDefaultAsync(item => item.Id == dto.SettingId, cancellationToken);
        if (setting is null)
        {
            return (false, "Ханшийн тохиргоо олдсонгүй.");
        }

        if (dto.BuyMarginPercent < 0 || dto.BuyMarginPercent > 20 || dto.SellMarginPercent < 0 || dto.SellMarginPercent > 20)
        {
            return (false, "Margin percent 0-20 хооронд байх ёстой.");
        }

        var oldBuyRate = setting.AlgoBuyRate;
        var oldSellRate = setting.AlgoSellRate;
        var oldManual = setting.IsManualOverride;
        var oldExpiresAt = setting.ManualExpiresAt;
        var buyMargin = dto.BuyMarginPercent / 100m;
        var sellMargin = dto.SellMarginPercent / 100m;

        setting.AlgoBuyMarginPercent = buyMargin;
        setting.AlgoSellMarginPercent = sellMargin;
        setting.AlgoBuyRate = CalculateBuyRate(setting.BaseRate, buyMargin);
        setting.AlgoSellRate = CalculateSellRate(setting.BaseRate, sellMargin);
        setting.UpdatedAt = MongoliaClock.Now;
        setting.UpdatedBy = adminUserId;

        AddCurrencyRateAudit(
            setting,
            "ALGORITHM_MARGIN_UPDATED",
            adminUserId,
            oldBuyRate,
            oldSellRate,
            setting.AlgoBuyRate,
            setting.AlgoSellRate,
            oldManual,
            setting.IsManualOverride,
            oldExpiresAt,
            setting.ManualExpiresAt,
            $"Algorithm margins updated. Buy={dto.BuyMarginPercent:N4}%, Sell={dto.SellMarginPercent:N4}%.");

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, "Алгоритмын авах/зарах margin амжилттай шинэчлэгдлээ.");
    }

    public async Task<(bool Success, string? ErrorMessage)> SetManualCurrencyRateOverrideAsync(
        long adminUserId,
        SetManualCurrencyRateOverrideDto dto,
        CancellationToken cancellationToken = default)
    {
        var setting = await _dbContext.CurrencyRateSettings.FirstOrDefaultAsync(item => item.Id == dto.SettingId, cancellationToken);
        if (setting is null)
        {
            return (false, "Ханшийн тохиргоо олдсонгүй.");
        }

        var mode = dto.AdjustmentMode.Trim().ToUpperInvariant();
        if (mode is not ("PERCENT" or "AMOUNT"))
        {
            return (false, "Manual override горим буруу байна.");
        }

        if (dto.BuyAdjustment < 0 || dto.SellAdjustment < 0)
        {
            return (false, "Нэмэгдүүлэх утга сөрөг байж болохгүй.");
        }

        if (mode == "PERCENT" && (dto.BuyAdjustment > 20 || dto.SellAdjustment > 20))
        {
            return (false, "Manual percent нэмэгдэл 0-20 хооронд байх ёстой.");
        }

        if (mode == "AMOUNT" && (dto.BuyAdjustment > 5000 || dto.SellAdjustment > 5000))
        {
            return (false, "Manual MNT нэмэгдэл 0-5000 хооронд байх ёстой.");
        }

        var startsAt = TrimToMinute(dto.StartsAt);
        var endsAt = TrimToMinute(dto.EndsAt);
        var now = TrimToMinute(MongoliaClock.Now);
        if (startsAt < now)
        {
            if (now - startsAt > TimeSpan.FromMinutes(2))
            {
                return (false, "Эхлэх огноо цаг одоогийн цагаас өмнө байж болохгүй.");
            }

            startsAt = now;
        }

        if (endsAt <= startsAt)
        {
            return (false, "Дуусах огноо цаг эхлэх огноо цагаас хойш байх ёстой.");
        }

        if (endsAt - startsAt > TimeSpan.FromDays(30))
        {
            return (false, "Manual override төлөвлөгөө хамгийн ихдээ 30 өдөр үргэлжилнэ.");
        }

        var hasOverlap = await _dbContext.CurrencyRateOverrideSchedules.AnyAsync(schedule =>
            schedule.CurrencyRateSettingId == setting.Id &&
            schedule.Status != "CANCELLED" &&
            startsAt < schedule.EndsAt &&
            endsAt > schedule.StartsAt,
            cancellationToken);
        if (hasOverlap)
        {
            return (false, "Энэ валют дээр тухайн хугацаанд давхцсан manual override төлөвлөгөө байна.");
        }

        var manualBuyRate = mode == "PERCENT"
            ? NormalizeBuyRate(setting.AlgoBuyRate * (1m + dto.BuyAdjustment / 100m))
            : NormalizeBuyRate(setting.AlgoBuyRate + dto.BuyAdjustment);
        var manualSellRate = mode == "PERCENT"
            ? NormalizeSellRate(setting.AlgoSellRate * (1m + dto.SellAdjustment / 100m))
            : NormalizeSellRate(setting.AlgoSellRate + dto.SellAdjustment);

        if (manualBuyRate <= 0 || manualSellRate <= 0)
        {
            return (false, "Manual авах/зарах ханш 0-ээс их байх ёстой.");
        }

        var schedule = new CurrencyRateOverrideSchedule
        {
            CurrencyRateSettingId = setting.Id,
            ManualBuyRate = manualBuyRate,
            ManualSellRate = manualSellRate,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Status = "SCHEDULED",
            CreatedBy = adminUserId,
            CreatedAt = MongoliaClock.Now,
            Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim()
        };

        _dbContext.CurrencyRateOverrideSchedules.Add(schedule);
        setting.UpdatedAt = now;
        setting.UpdatedBy = adminUserId;

        AddCurrencyRateAudit(
            setting,
            "MANUAL_OVERRIDE_SCHEDULED",
            adminUserId,
            setting.AlgoBuyRate,
            setting.AlgoSellRate,
            manualBuyRate,
            manualSellRate,
            false,
            true,
            null,
            endsAt,
            $"Manual override scheduled by {mode}. StartsAt={startsAt:yyyy-MM-dd HH:mm}, EndsAt={endsAt:yyyy-MM-dd HH:mm}, BuyAdjustment={dto.BuyAdjustment}, SellAdjustment={dto.SellAdjustment}.");

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.GetBaseException().Message.Contains("Overlapping currency rate override schedule", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Энэ валют дээр тухайн хугацаанд давхцсан manual override төлөвлөгөө байна.");
        }

        return (true, "Manual авах/зарах ханшийн төлөвлөгөө амжилттай үүслээ.");
    }

    public async Task<(bool Success, string? ErrorMessage)> CancelCurrencyRateOverrideScheduleAsync(
        long adminUserId,
        long scheduleId,
        CancellationToken cancellationToken = default)
    {
        var schedule = await _dbContext.CurrencyRateOverrideSchedules
            .Include(item => item.CurrencyRateSetting)
            .FirstOrDefaultAsync(item => item.Id == scheduleId, cancellationToken);
        if (schedule is null)
        {
            return (false, "Manual override төлөвлөгөө олдсонгүй.");
        }

        if (schedule.Status == "CANCELLED")
        {
            return (true, "Manual override төлөвлөгөө аль хэдийн цуцлагдсан байна.");
        }

        var setting = schedule.CurrencyRateSetting;
        var now = MongoliaClock.Now;
        schedule.Status = "CANCELLED";
        schedule.CancelledBy = adminUserId;
        schedule.CancelledAt = now;
        setting.UpdatedAt = now;
        setting.UpdatedBy = adminUserId;

        AddCurrencyRateAudit(
            setting,
            "MANUAL_OVERRIDE_CANCELLED",
            adminUserId,
            schedule.ManualBuyRate,
            schedule.ManualSellRate,
            null,
            null,
            true,
            false,
            schedule.EndsAt,
            null,
            $"Manual override schedule #{schedule.Id} cancelled by admin.");

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, "Manual override төлөвлөгөө цуцлагдлаа.");
    }

    public async Task<AdminPagedResultDto<AdminSuspiciousTransactionDto>> GetSuspiciousTransactionsAsync(
        AdminSuspiciousTransactionFilterDto? filter = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = QuerySuspiciousDetails();
        var normalizedSearch = NormalizeSearch(filter?.Search);
        if (normalizedSearch is not null)
        {
            query = query.Where(detail =>
                detail.Transaction.FromAccount.AccountNumber.Contains(normalizedSearch) ||
                detail.Transaction.ToAccount.AccountNumber.Contains(normalizedSearch) ||
                detail.Transaction.FromAccount.User.Username.Contains(normalizedSearch) ||
                detail.Transaction.ToAccount.User.Username.Contains(normalizedSearch) ||
                detail.SuspiciousReason.Contains(normalizedSearch) ||
                (detail.ReviewNote ?? "").Contains(normalizedSearch));
        }

        if (ReviewStatusHelper.IsValid(filter?.ReviewStatus))
        {
            var normalizedStatus = ReviewStatusHelper.Normalize(filter!.ReviewStatus!);
            query = query.Where(detail => detail.ReviewStatus == normalizedStatus);
        }

        var accountNumber = NormalizeSearch(filter?.AccountNumber);
        if (accountNumber is not null)
        {
            query = query.Where(detail =>
                detail.Transaction.FromAccount.AccountNumber.Contains(accountNumber) ||
                detail.Transaction.ToAccount.AccountNumber.Contains(accountNumber));
        }

        var username = NormalizeSearch(filter?.Username);
        if (username is not null)
        {
            query = query.Where(detail =>
                detail.Transaction.FromAccount.User.Username.Contains(username) ||
                detail.Transaction.ToAccount.User.Username.Contains(username) ||
                ((detail.Transaction.FromAccount.User.FirstName ?? "") + " " + (detail.Transaction.FromAccount.User.LastName ?? "")).Contains(username) ||
                ((detail.Transaction.ToAccount.User.FirstName ?? "") + " " + (detail.Transaction.ToAccount.User.LastName ?? "")).Contains(username));
        }

        var currency = NormalizeSearch(filter?.Currency)?.ToUpperInvariant();
        if (currency is not null)
        {
            query = query.Where(detail => detail.Transaction.SourceCurrency == currency || detail.Transaction.TargetCurrency == currency);
        }

        var effectiveMinRiskScore = filter?.MinRiskScore ?? await GetFraudDetectionThresholdAsync(cancellationToken);
        query = query.Where(detail => detail.RiskScore >= effectiveMinRiskScore);

        if (filter?.MaxRiskScore is decimal maxRiskScore)
        {
            query = query.Where(detail => detail.RiskScore <= maxRiskScore);
        }

        var minAmount = filter?.MinAmount;
        var maxAmount = filter?.MaxAmount;
        if (minAmount is not null || maxAmount is not null)
        {
            if (currency is not null)
            {
                query = query.Where(detail =>
                    (detail.Transaction.SourceCurrency == currency &&
                     (minAmount == null || detail.Transaction.Amount >= minAmount.Value) &&
                     (maxAmount == null || detail.Transaction.Amount <= maxAmount.Value)) ||
                    (detail.Transaction.TargetCurrency == currency &&
                     (minAmount == null || detail.Transaction.CreditedAmount >= minAmount.Value) &&
                     (maxAmount == null || detail.Transaction.CreditedAmount <= maxAmount.Value)));
            }
            else
            {
                query = query.Where(detail =>
                    (minAmount == null || detail.Transaction.Amount >= minAmount.Value) &&
                    (maxAmount == null || detail.Transaction.Amount <= maxAmount.Value));
            }
        }

        if (filter?.StartDate is DateOnly startDate)
        {
            var start = startDate.ToDateTime(TimeOnly.MinValue);
            query = query.Where(detail => detail.Transaction.CreatedAt >= start);
        }

        if (filter?.EndDate is DateOnly endDate)
        {
            var endExclusive = endDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(detail => detail.Transaction.CreatedAt < endExclusive);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var pageInfo = NormalizePage(page, pageSize, totalItems);
        var details = await query
            .OrderByDescending(detail => detail.CreatedAt)
            .ThenByDescending(detail => detail.TransactionId)
            .Skip((pageInfo.Page - 1) * pageInfo.PageSize)
            .Take(pageInfo.PageSize)
            .ToListAsync(cancellationToken);

        return new AdminPagedResultDto<AdminSuspiciousTransactionDto>
        {
            Items = details.Select(MapSuspiciousDetail).ToList(),
            Page = pageInfo.Page,
            PageSize = pageInfo.PageSize,
            TotalItems = totalItems
        };
    }

    public async Task<AdminSuspiciousTransactionDto?> GetSuspiciousTransactionDetailAsync(long transactionId, CancellationToken cancellationToken = default)
    {
        var detail = await QuerySuspiciousDetails()
            .FirstOrDefaultAsync(detail => detail.TransactionId == transactionId, cancellationToken);

        if (detail is not null)
        {
            return MapSuspiciousDetail(detail);
        }

        var transaction = await _dbContext.Transactions
            .AsNoTracking()
            .Include(item => item.FromAccount)
                .ThenInclude(account => account.User)
            .Include(item => item.ToAccount)
                .ThenInclude(account => account.User)
            .Include(item => item.TransactionDetectionLogs)
            .Include(item => item.AiTransactionAnalysisLogs)
            .FirstOrDefaultAsync(item => item.Id == transactionId, cancellationToken);

        return transaction is null ? null : MapSuspiciousReviewDraft(transaction);
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateSuspiciousReviewAsync(
        long adminUserId,
        UpdateSuspiciousReviewDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!ReviewStatusHelper.IsValid(dto.ReviewStatus))
        {
            return (false, "Буруу review status байна.");
        }

        var detail = await _dbContext.SuspiciousTransactionDetails
            .Include(item => item.Transaction)
                .ThenInclude(transaction => transaction.FromAccount)
                    .ThenInclude(account => account.User)
            .Include(item => item.Transaction)
                .ThenInclude(transaction => transaction.ToAccount)
                    .ThenInclude(account => account.User)
            .FirstOrDefaultAsync(item => item.TransactionId == dto.TransactionId, cancellationToken);

        var isNewReview = detail is null;
        if (detail is null)
        {
            var transaction = await _dbContext.Transactions
                .Include(item => item.FromAccount)
                    .ThenInclude(account => account.User)
                .Include(item => item.ToAccount)
                    .ThenInclude(account => account.User)
                .Include(item => item.TransactionDetectionLogs)
                .Include(item => item.AiTransactionAnalysisLogs)
                .FirstOrDefaultAsync(item => item.Id == dto.TransactionId, cancellationToken);
            if (transaction is null)
            {
                return (false, "Гүйлгээ олдсонгүй.");
            }

            var latestDetection = transaction.TransactionDetectionLogs
                .OrderByDescending(log => log.CreatedAt)
                .FirstOrDefault();
            var latestAi = transaction.AiTransactionAnalysisLogs
                .OrderByDescending(log => log.CreatedAt)
                .FirstOrDefault();
            var createdAt = MongoliaClock.Now;
            detail = new SuspiciousTransactionDetail
            {
                TransactionId = transaction.Id,
                Transaction = transaction,
                RiskScore = latestAi?.RiskScore ?? latestDetection?.RiskScore ?? 0m,
                SuspiciousReason = latestAi?.Explanation ?? latestDetection?.Reason ?? "Admin review workflow.",
                AiExplanation = latestAi?.Explanation,
                ReviewStatus = "REVIEWING",
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            };
            transaction.IsSuspicious = true;
            _dbContext.SuspiciousTransactionDetails.Add(detail);
        }

        if (dto.ExpectedUpdatedAtTicks is not null && detail.UpdatedAt.Ticks != dto.ExpectedUpdatedAtTicks.Value)
        {
            return (false, "Энэ review өөр admin-аар шинэчлэгдсэн байна. Дахин нээгээд шинэ мэдээллээр үргэлжлүүлнэ үү.");
        }

        var oldValue = new
        {
            detail.ReviewStatus,
            detail.ReviewNote,
            detail.ReviewedBy,
            detail.ReviewedAt
        };

        var now = MongoliaClock.Now;
        detail.ReviewStatus = ReviewStatusHelper.Normalize(dto.ReviewStatus);
        detail.ReviewNote = string.IsNullOrWhiteSpace(dto.ReviewNote) ? null : dto.ReviewNote.Trim();
        detail.ReviewedBy = adminUserId;
        detail.ReviewedAt = now;
        detail.UpdatedAt = now;

        var senderAccount = detail.Transaction.FromAccount;
        var receiverAccount = detail.Transaction.ToAccount;
        var senderUser = senderAccount.User;
        var receiverUser = receiverAccount.User;

        if ((dto.DeactivateSenderUser && senderUser.Id == adminUserId) ||
            (dto.DeactivateReceiverUser && receiverUser.Id == adminUserId))
        {
            return (false, "Өөрийн admin эрхийг энэ workflow-оор идэвхгүй болгох боломжгүй.");
        }

        if (dto.DeactivateSenderAccount)
        {
            DeactivateSuspiciousAccount(adminUserId, senderAccount, detail.TransactionId, "SENDER_ACCOUNT_DEACTIVATED");
        }

        if (dto.DeactivateReceiverAccount && receiverAccount.Id != senderAccount.Id)
        {
            DeactivateSuspiciousAccount(adminUserId, receiverAccount, detail.TransactionId, "RECEIVER_ACCOUNT_DEACTIVATED");
        }

        if (dto.DeactivateSenderUser)
        {
            DeactivateSuspiciousUser(adminUserId, senderUser, detail.TransactionId, "SENDER_USER_DEACTIVATED");
        }

        if (dto.DeactivateReceiverUser && receiverUser.Id != senderUser.Id)
        {
            DeactivateSuspiciousUser(adminUserId, receiverUser, detail.TransactionId, "RECEIVER_USER_DEACTIVATED");
        }

        AddAuditLog(
            adminUserId,
            isNewReview ? "SUSPICIOUS_REVIEW_CREATED_FROM_AI" : "SUSPICIOUS_REVIEW_UPDATED",
            isNewReview ? "transactions" : "suspicious_transaction_details",
            isNewReview ? detail.TransactionId : detail.Id,
            oldValue,
            new
            {
                detail.ReviewStatus,
                detail.ReviewNote,
                detail.ReviewedBy,
                detail.ReviewedAt
            },
            isNewReview
                ? $"Admin confirmed and created a suspicious review workflow for transaction #{detail.TransactionId}."
                : $"Transaction #{detail.TransactionId} review updated.");

        var notification = dto.SendUserNotification
            ? BuildReviewNotification(detail, dto.UserNotificationMessage)
            : null;
        if (notification is not null)
        {
            _dbContext.Notifications.Add(notification);
        }

        if (dto.NotifySender)
        {
            _dbContext.Notifications.Add(BuildFraudActionNotification(
                senderUser.Id,
                detail.TransactionId,
                "Гүйлгээний аюулгүй байдлын мэдэгдэл",
                dto.SenderNotificationMessage,
                $"Таны {senderAccount.AccountNumber} данстай холбоотой гүйлгээнд аюулгүй байдлын нэмэлт шалгалт хийгдлээ. Шаардлагатай бол банкны ажилтантай холбогдоно уу."));
        }

        if (dto.NotifyReceiver && receiverUser.Id != senderUser.Id)
        {
            _dbContext.Notifications.Add(BuildFraudActionNotification(
                receiverUser.Id,
                detail.TransactionId,
                "Гүйлгээний аюулгүй байдлын мэдэгдэл",
                dto.ReceiverNotificationMessage,
                $"Таны {receiverAccount.AccountNumber} данс руу орсон гүйлгээнд аюулгүй байдлын нэмэлт шалгалт хийгдлээ. Шаардлагатай бол банкны ажилтантай холбогдоно уу."));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, "Review status амжилттай шинэчлэгдлээ.");
    }

    public async Task<(bool Success, string? ErrorMessage)> SetUserActiveStatusAsync(
        long adminUserId,
        long userId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        if (adminUserId == userId && !isActive)
        {
            return (false, "Өөрийн admin эрхийг идэвхгүй болгох боломжгүй.");
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
        if (user is null)
        {
            return (false, "Хэрэглэгч олдсонгүй.");
        }

        var oldValue = new { user.IsActive };
        user.IsActive = isActive;
        user.UpdatedAt = MongoliaClock.Now;

        AddAuditLog(
            adminUserId,
            "USER_STATUS_UPDATED",
            "users",
            user.Id,
            oldValue,
            new { user.IsActive },
            isActive
                ? $"User {user.Username} was enabled and sign-in access was restored."
                : $"User {user.Username} was disabled and sign-in access was blocked.");

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, "Хэрэглэгчийн төлөв амжилттай шинэчлэгдлээ.");
    }

    public async Task<(bool Success, string? ErrorMessage)> SetAccountActiveStatusAsync(
        long adminUserId,
        long accountId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.Accounts.FirstOrDefaultAsync(account => account.Id == accountId, cancellationToken);
        if (account is null)
        {
            return (false, "Данс олдсонгүй.");
        }

        var oldValue = new
        {
            account.IsActive,
            account.IsAdminLocked,
            account.AdminLockedAt,
            account.AdminLockedByUserId
        };
        var now = MongoliaClock.Now;
        account.IsActive = isActive;
        account.IsAdminLocked = !isActive;
        account.AdminLockedAt = isActive ? null : now;
        account.AdminLockedByUserId = isActive ? null : adminUserId;
        account.UpdatedAt = now;

        AddAuditLog(
            adminUserId,
            "ACCOUNT_STATUS_UPDATED",
            "accounts",
            account.Id,
            oldValue,
            new
            {
                account.IsActive,
                account.IsAdminLocked,
                account.AdminLockedAt,
                account.AdminLockedByUserId
            },
            isActive
                ? $"Account {account.AccountNumber} was activated and can be used for transactions."
                : $"Account {account.AccountNumber} was deactivated and cannot be used for transactions.");

        _dbContext.Notifications.Add(new Notification
        {
            UserId = account.UserId,
            TransactionId = null,
            NotificationType = "ACCOUNT_STATUS_UPDATED",
            Title = "Дансны төлөв өөрчлөгдлөө",
            Message = isActive
                ? $"Таны {account.AccountNumber} дугаартай данс идэвхтэй боллоо."
                : $"Таны {account.AccountNumber} дугаартай данс идэвхгүй боллоо.",
            IsRead = false,
            CreatedAt = account.UpdatedAt
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, "Дансны төлөв амжилттай шинэчлэгдлээ.");
    }

    private IQueryable<AuditLog> BuildAuditLogQuery(
        string source,
        string? search,
        DateTime? start,
        DateTime? endExclusive)
    {
        var query = _dbContext.AuditLogs
            .AsNoTracking()
            .Include(log => log.User)
            .AsQueryable();

        if (source == "ADMIN")
        {
            query = query.Where(log => !(log.Action.Contains("SUSPICIOUS") || log.TargetType == "suspicious_transaction_details"));
        }
        else if (source == "FRAUD")
        {
            query = query.Where(log => log.Action.Contains("SUSPICIOUS") || log.TargetType == "suspicious_transaction_details");
        }
        else if (source != "ALL")
        {
            query = query.Where(_ => false);
        }

        if (start is not null)
        {
            query = query.Where(log => log.CreatedAt >= start.Value);
        }

        if (endExclusive is not null)
        {
            query = query.Where(log => log.CreatedAt < endExclusive.Value);
        }

        if (search is not null)
        {
            query = query.Where(log =>
                log.Action.Contains(search) ||
                (log.TargetType ?? "").Contains(search) ||
                (log.Detail ?? "").Contains(search) ||
                (log.IpAddress ?? "").Contains(search) ||
                (log.User != null && (
                    log.User.Username.Contains(search) ||
                    log.User.Email.Contains(search) ||
                    ((log.User.FirstName ?? "") + " " + (log.User.LastName ?? "")).Contains(search))));
        }

        return query;
    }

    private IQueryable<SecurityEventLog> BuildSecurityEventQuery(
        string source,
        string? search,
        DateTime? start,
        DateTime? endExclusive)
    {
        var query = _dbContext.SecurityEventLogs
            .AsNoTracking()
            .Include(log => log.User)
            .AsQueryable();

        if (source is not ("ALL" or "SECURITY"))
        {
            query = query.Where(_ => false);
        }

        if (start is not null)
        {
            query = query.Where(log => log.CreatedAt >= start.Value);
        }

        if (endExclusive is not null)
        {
            query = query.Where(log => log.CreatedAt < endExclusive.Value);
        }

        if (search is not null)
        {
            query = query.Where(log =>
                log.EventType.Contains(search) ||
                (log.UsernameOrEmail ?? "").Contains(search) ||
                (log.Message ?? "").Contains(search) ||
                (log.IpAddress ?? "").Contains(search) ||
                (log.User != null && (
                    log.User.Username.Contains(search) ||
                    log.User.Email.Contains(search) ||
                    ((log.User.FirstName ?? "") + " " + (log.User.LastName ?? "")).Contains(search))));
        }

        return query;
    }

    private IQueryable<CurrencyRateSettingAudit> BuildCurrencyRateAuditQuery(
        string source,
        string? search,
        DateTime? start,
        DateTime? endExclusive)
    {
        var query = _dbContext.CurrencyRateSettingAudits
            .AsNoTracking()
            .Include(log => log.ChangedByNavigation)
            .Include(log => log.CurrencyRateSetting)
            .AsQueryable();

        if (source is not ("ALL" or "RATE"))
        {
            query = query.Where(_ => false);
        }

        if (start is not null)
        {
            query = query.Where(log => log.ChangedAt >= start.Value);
        }

        if (endExclusive is not null)
        {
            query = query.Where(log => log.ChangedAt < endExclusive.Value);
        }

        if (search is not null)
        {
            query = query.Where(log =>
                log.Action.Contains(search) ||
                (log.Note ?? "").Contains(search) ||
                log.CurrencyRateSetting.CurrencyCode.Contains(search) ||
                log.CurrencyRateSetting.BaseCurrency.Contains(search) ||
                (log.ChangedByNavigation != null && (
                    log.ChangedByNavigation.Username.Contains(search) ||
                    log.ChangedByNavigation.Email.Contains(search) ||
                    ((log.ChangedByNavigation.FirstName ?? "") + " " + (log.ChangedByNavigation.LastName ?? "")).Contains(search))));
        }

        return query;
    }

    private IQueryable<TransactionDetectionLog> BuildFraudDetectionQuery(
        string source,
        string? search,
        DateTime? start,
        DateTime? endExclusive)
    {
        var query = _dbContext.TransactionDetectionLogs
            .AsNoTracking()
            .Include(log => log.Transaction)
                .ThenInclude(transaction => transaction.FromAccount)
            .Include(log => log.Transaction)
                .ThenInclude(transaction => transaction.ToAccount)
            .AsQueryable();

        if (source is not ("ALL" or "FRAUD"))
        {
            query = query.Where(_ => false);
        }

        if (start is not null)
        {
            query = query.Where(log => log.CreatedAt >= start.Value);
        }

        if (endExclusive is not null)
        {
            query = query.Where(log => log.CreatedAt < endExclusive.Value);
        }

        if (search is not null)
        {
            query = query.Where(log =>
                log.ServiceStatus.Contains(search) ||
                log.Source.Contains(search) ||
                (log.Reason ?? "").Contains(search) ||
                (log.TriggeredRules ?? "").Contains(search) ||
                log.Transaction.FromAccount.AccountNumber.Contains(search) ||
                log.Transaction.ToAccount.AccountNumber.Contains(search));
        }

        return query;
    }

    private IQueryable<TransactionDetectionLog> BuildSuspiciousDetectionReportQuery(
        DateTime start,
        DateTime endExclusive,
        string? search,
        string? reviewStatus,
        bool suspiciousOnly)
    {
        var query = _dbContext.TransactionDetectionLogs
            .AsNoTracking()
            .AsQueryable();

        query = query.Where(log => log.CreatedAt >= start && log.CreatedAt < endExclusive);

        if (suspiciousOnly)
        {
            query = query.Where(log => log.IsSuspicious == true);
        }

        if (!string.IsNullOrWhiteSpace(reviewStatus))
        {
            query = query.Where(log =>
                log.Transaction.SuspiciousTransactionDetail != null &&
                log.Transaction.SuspiciousTransactionDetail.ReviewStatus == reviewStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(log =>
                log.TransactionId.ToString().Contains(search) ||
                log.ServiceStatus.Contains(search) ||
                log.Source.Contains(search) ||
                (log.Reason ?? "").Contains(search) ||
                (log.TriggeredRules ?? "").Contains(search) ||
                log.Transaction.FromAccount.AccountNumber.Contains(search) ||
                log.Transaction.ToAccount.AccountNumber.Contains(search) ||
                (log.Transaction.Description ?? "").Contains(search));
        }

        return query;
    }

    private static List<string> ParseTriggeredRules(string? triggeredRules)
    {
        if (string.IsNullOrWhiteSpace(triggeredRules))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(triggeredRules) ?? [];
        }
        catch (JsonException)
        {
            return triggeredRules
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }
    }

    private static AdminAuditLogDto MapAuditLog(AuditLog log)
    {
        var isFraudAction = log.Action.Contains("SUSPICIOUS", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(log.TargetType, "suspicious_transaction_details", StringComparison.OrdinalIgnoreCase);

        return new AdminAuditLogDto
        {
            Source = isFraudAction ? "FRAUD" : "ADMIN",
            SourceLabel = isFraudAction ? "Fraud action" : "Admin action",
            SourceId = log.Id,
            OccurredAt = log.CreatedAt,
            ActorUserId = log.UserId,
            ActorDisplayName = BuildUserDisplay(log.User, log.UserId),
            Action = log.Action,
            ActionLabel = GetAuditActionLabel(log.Action),
            TargetType = log.TargetType,
            TargetId = log.TargetId,
            Summary = BuildAuditDisplayDetail(log.Action, log.Detail, log.NewValue),
            Detail = BuildAuditDisplayDetail(log.Action, log.Detail, log.NewValue),
            OldValue = log.OldValue,
            NewValue = log.NewValue,
            IpAddress = log.IpAddress,
            UserAgent = log.UserAgent,
            Severity = isFraudAction ? "WARNING" : "INFO"
        };
    }

    private static AdminAuditLogDto MapSecurityEventLog(SecurityEventLog log)
    {
        var isHighRisk = !log.Success ||
                         log.EventType.Contains("LOCK", StringComparison.OrdinalIgnoreCase) ||
                         log.EventType.Contains("FAILED", StringComparison.OrdinalIgnoreCase);

        return new AdminAuditLogDto
        {
            Source = "SECURITY",
            SourceLabel = "Security event",
            SourceId = log.Id,
            OccurredAt = log.CreatedAt,
            ActorUserId = log.UserId,
            ActorDisplayName = BuildUserDisplay(log.User, log.UserId, log.UsernameOrEmail),
            Action = log.EventType,
            ActionLabel = HumanizeAction(log.EventType),
            TargetType = "security_event_logs",
            TargetId = log.Id,
            Summary = log.Message ?? HumanizeAction(log.EventType),
            Detail = log.Message,
            IpAddress = log.IpAddress,
            UserAgent = log.UserAgent,
            Severity = isHighRisk ? "WARNING" : "INFO",
            Success = log.Success
        };
    }

    private static AdminAuditLogDto MapCurrencyRateAudit(CurrencyRateSettingAudit log)
    {
        var pair = $"{log.CurrencyRateSetting.CurrencyCode}/{log.CurrencyRateSetting.BaseCurrency}";
        return new AdminAuditLogDto
        {
            Source = "RATE",
            SourceLabel = "Rate change",
            SourceId = log.Id,
            OccurredAt = log.ChangedAt,
            ActorUserId = log.ChangedBy,
            ActorDisplayName = BuildUserDisplay(log.ChangedByNavigation, log.ChangedBy),
            Action = log.Action,
            ActionLabel = HumanizeAction(log.Action),
            TargetType = "currency_rate_settings",
            TargetId = log.CurrencyRateSettingId,
            Summary = $"{pair}: {HumanizeAction(log.Action)}",
            Detail = log.Note,
            OldValue = $"buy={FormatNullableRate(log.OldBuyRate)}, sell={FormatNullableRate(log.OldSellRate)}, manual={log.OldIsManualOverride}, expires={FormatNullableDate(log.OldManualExpiresAt)}",
            NewValue = $"buy={FormatNullableRate(log.NewBuyRate)}, sell={FormatNullableRate(log.NewSellRate)}, manual={log.NewIsManualOverride}, expires={FormatNullableDate(log.NewManualExpiresAt)}",
            Severity = log.Action.Contains("MANUAL", StringComparison.OrdinalIgnoreCase) ? "NOTICE" : "INFO"
        };
    }

    private static AdminAuditLogDto MapFraudDetectionLog(TransactionDetectionLog log)
    {
        var isWarning = log.IsSuspicious == true ||
                        !string.Equals(log.ServiceStatus, "CHECKED", StringComparison.OrdinalIgnoreCase);

        return new AdminAuditLogDto
        {
            Source = "FRAUD",
            SourceLabel = "Fraud detection",
            SourceId = log.Id,
            OccurredAt = log.CreatedAt,
            ActorDisplayName = log.Source,
            Action = log.ServiceStatus,
            ActionLabel = log.IsSuspicious == true ? "Suspicious detected" : HumanizeAction(log.ServiceStatus),
            TargetType = "transactions",
            TargetId = log.TransactionId,
            Summary = $"Transaction #{log.TransactionId}: {log.Transaction.FromAccount.AccountNumber} -> {log.Transaction.ToAccount.AccountNumber}",
            Detail = log.Reason,
            NewValue = $"risk={log.RiskScore?.ToString("N2") ?? "-"}, suspicious={log.IsSuspicious}, rules={log.TriggeredRules ?? "-"}",
            Severity = isWarning ? "WARNING" : "INFO",
            Success = string.Equals(log.ServiceStatus, "CHECKED", StringComparison.OrdinalIgnoreCase)
        };
    }

    private static string NormalizeAuditSource(string? source)
    {
        var normalized = source?.Trim().ToUpperInvariant();
        return normalized is "SECURITY" or "ADMIN" or "RATE" or "FRAUD" ? normalized : "ALL";
    }

    private static string BuildUserDisplay(User? user, long? userId, string? fallback = null)
    {
        if (user is not null)
        {
            var fullName = UserDisplayNameFormatter.Format(user.FirstName, user.LastName);
            return string.IsNullOrWhiteSpace(fullName)
                ? user.Username
                : $"{fullName} ({user.Username})";
        }

        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return fallback;
        }

        return userId is null ? "system" : $"user #{userId}";
    }

    private static string BuildUserDisplayName(User user)
    {
        return UserDisplayNameFormatter.Format(user.FirstName, user.LastName, user.Username);
    }

    private static string HumanizeAction(string value)
    {
        return value.Replace('_', ' ').ToLowerInvariant();
    }

    private static string GetAuditActionLabel(string action)
    {
        return action switch
        {
            "ADMIN_USER_PROFILE_UPDATED" => "User profile updated",
            "ADMIN_USER_PASSWORD_RESET_REQUIRED_UPDATED" => "Password reset requirement updated",
            "ADMIN_USER_UNLOCKED" => "User lock cleared",
            "ACCOUNT_TRANSACTION_LIMIT_UPDATED" => "Daily transaction limit updated",
            "AI_TRANSACTION_ANALYSIS" => "AI transaction analysis generated",
            "AI_TRANSACTION_CHAT" => "AI transaction chat used",
            "FRAUD_RULE_SETTING_UPDATED" => "Fraud rule setting updated",
            "FRAUD_DETECTION_SETTING_UPDATED" => "Fraud detection threshold updated",
            "ALGORITHM_MARGIN_UPDATED" => "Exchange-rate algorithm margin updated",
            "SUSPICIOUS_REVIEW_CREATED_FROM_AI" => "Suspicious review created from AI detection",
            "SUSPICIOUS_REVIEW_UPDATED" => "Suspicious transaction review updated",
            "USER_STATUS_UPDATED" => "User access status updated",
            "ACCOUNT_STATUS_UPDATED" => "Account status updated",
            "SENDER_ACCOUNT_DEACTIVATED" => "Sender account deactivated",
            "RECEIVER_ACCOUNT_DEACTIVATED" => "Receiver account deactivated",
            "SENDER_USER_DEACTIVATED" => "Sender user deactivated",
            "RECEIVER_USER_DEACTIVATED" => "Receiver user deactivated",
            _ => HumanizeAction(action)
        };
    }

    private static string BuildAuditDisplayDetail(string action, string? detail, string? newValue)
    {
        return action switch
        {
            "USER_STATUS_UPDATED" => TryReadBoolean(newValue, "IsActive") switch
            {
                false => "The admin disabled this user account and blocked future sign-ins.",
                true => "The admin enabled this user account and restored sign-in access.",
                _ => "The admin changed this user's access status."
            },
            "ACCOUNT_STATUS_UPDATED" => TryReadBoolean(newValue, "IsActive") switch
            {
                false => "The admin deactivated this account. The account can no longer be used for transactions.",
                true => "The admin activated this account. The account can be used for transactions again.",
                _ => "The admin changed this account's active status."
            },
            "ADMIN_USER_PROFILE_UPDATED" => "The admin updated this user's profile and contact information.",
            "ADMIN_USER_PASSWORD_RESET_REQUIRED_UPDATED" => TryReadBoolean(newValue, "PasswordResetRequired") switch
            {
                true => "The admin required this user to change their password at the next sign-in.",
                false => "The admin cancelled the password reset requirement for this user.",
                _ => "The admin changed this user's password reset requirement."
            },
            "ADMIN_USER_UNLOCKED" => "The admin cleared the temporary login lock and reset the failed login counter.",
            "ACCOUNT_TRANSACTION_LIMIT_UPDATED" => "The admin updated the account's daily total transaction limit.",
            "AI_TRANSACTION_ANALYSIS" => "The admin ran Gemini AI analysis for this transaction.",
            "AI_TRANSACTION_CHAT" => "The admin asked a follow-up AI question about this transaction.",
            "FRAUD_RULE_SETTING_UPDATED" => string.IsNullOrWhiteSpace(detail) ? "The admin updated a fraud detection rule score, threshold, or enabled status." : detail,
            "FRAUD_DETECTION_SETTING_UPDATED" => "The admin updated the global rule-based suspicious score threshold.",
            "SUSPICIOUS_REVIEW_CREATED_FROM_AI" => "The admin created a suspicious transaction review workflow from the AI Detection page.",
            "SUSPICIOUS_REVIEW_UPDATED" => "The admin updated the suspicious transaction review status, note, notification, or enforcement action.",
            "SENDER_ACCOUNT_DEACTIVATED" => "The sender account was deactivated as part of the suspicious transaction review workflow.",
            "RECEIVER_ACCOUNT_DEACTIVATED" => "The receiver account was deactivated as part of the suspicious transaction review workflow.",
            "SENDER_USER_DEACTIVATED" => "The sender user's sign-in access was disabled as part of the suspicious transaction review workflow.",
            "RECEIVER_USER_DEACTIVATED" => "The receiver user's sign-in access was disabled as part of the suspicious transaction review workflow.",
            _ => string.IsNullOrWhiteSpace(detail) ? GetAuditActionLabel(action) : detail
        };
    }

    private static bool? TryReadBoolean(string? json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty(propertyName, out var property) &&
                (property.ValueKind is JsonValueKind.True or JsonValueKind.False))
            {
                return property.GetBoolean();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string FormatNullableRate(decimal? value)
    {
        return value?.ToString("N4") ?? "-";
    }

    private static string FormatNullableDate(DateTime? value)
    {
        return value?.ToString("yyyy-MM-dd HH:mm") ?? "-";
    }

    private static AdminCurrencyRateSettingDto MapCurrencyRateSetting(CurrencyRateSetting setting, DateTime now)
    {
        var activeSchedule = GetActiveSchedule(setting, now);
        var isLegacyManualActive = activeSchedule is null &&
                                   setting.IsManualOverride &&
                                   setting.ManualBuyRate is not null &&
                                   setting.ManualSellRate is not null &&
                                   (setting.ManualExpiresAt is null || setting.ManualExpiresAt > now);
        var isManualActive = activeSchedule is not null || isLegacyManualActive;

        return new AdminCurrencyRateSettingDto
        {
            Id = setting.Id,
            CurrencyCode = setting.CurrencyCode,
            BaseCurrency = setting.BaseCurrency,
            BaseRate = setting.BaseRate,
            AlgoBuyMarginPercent = setting.AlgoBuyMarginPercent * 100m,
            AlgoSellMarginPercent = setting.AlgoSellMarginPercent * 100m,
            AlgoBuyRate = setting.AlgoBuyRate,
            AlgoSellRate = setting.AlgoSellRate,
            IsManualOverride = setting.IsManualOverride,
            IsManualOverrideActive = isManualActive,
            ManualBuyRate = activeSchedule?.ManualBuyRate ?? setting.ManualBuyRate,
            ManualSellRate = activeSchedule?.ManualSellRate ?? setting.ManualSellRate,
            ManualExpiresAt = activeSchedule?.EndsAt ?? setting.ManualExpiresAt,
            ActiveBuyRate = activeSchedule?.ManualBuyRate ?? (isLegacyManualActive ? setting.ManualBuyRate!.Value : setting.AlgoBuyRate),
            ActiveSellRate = activeSchedule?.ManualSellRate ?? (isLegacyManualActive ? setting.ManualSellRate!.Value : setting.AlgoSellRate),
            RateDate = setting.RateDate,
            Source = setting.Source,
            FetchedAt = setting.FetchedAt,
            UpdatedAt = setting.UpdatedAt,
            UpdatedByUsername = setting.UpdatedByNavigation?.Username,
            OverrideSchedules = setting.CurrencyRateOverrideSchedules
                .OrderBy(schedule => schedule.StartsAt)
                .ThenBy(schedule => schedule.Id)
                .Select(schedule => MapCurrencyRateOverrideSchedule(setting, schedule, now))
                .ToList()
        };
    }

    private static AdminCurrencyRateOverrideScheduleDto MapCurrencyRateOverrideSchedule(
        CurrencyRateSetting setting,
        CurrencyRateOverrideSchedule schedule,
        DateTime now)
    {
        return new AdminCurrencyRateOverrideScheduleDto
        {
            Id = schedule.Id,
            SettingId = setting.Id,
            CurrencyCode = setting.CurrencyCode,
            BaseCurrency = setting.BaseCurrency,
            ManualBuyRate = schedule.ManualBuyRate,
            ManualSellRate = schedule.ManualSellRate,
            StartsAt = schedule.StartsAt,
            EndsAt = schedule.EndsAt,
            Status = schedule.Status,
            DisplayStatus = GetScheduleDisplayStatus(schedule, now),
            CreatedByUsername = schedule.CreatedByNavigation?.Username,
            CreatedAt = schedule.CreatedAt,
            CancelledByUsername = schedule.CancelledByNavigation?.Username,
            CancelledAt = schedule.CancelledAt,
            Note = schedule.Note
        };
    }

    private static CurrencyRateOverrideSchedule? GetActiveSchedule(CurrencyRateSetting setting, DateTime now)
    {
        return setting.CurrencyRateOverrideSchedules
            .Where(schedule =>
                schedule.Status != "CANCELLED" &&
                schedule.StartsAt <= now &&
                schedule.EndsAt > now)
            .OrderByDescending(schedule => schedule.StartsAt)
            .FirstOrDefault();
    }

    private static string GetScheduleDisplayStatus(CurrencyRateOverrideSchedule schedule, DateTime now)
    {
        if (schedule.Status == "CANCELLED")
        {
            return "CANCELLED";
        }

        if (schedule.StartsAt > now)
        {
            return "SCHEDULED";
        }

        return schedule.EndsAt <= now ? "EXPIRED" : "ACTIVE";
    }

    private static DateTime TrimToMinute(DateTime value)
    {
        return new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Kind);
    }

    private void AddCurrencyRateAudit(
        CurrencyRateSetting setting,
        string action,
        long? changedBy,
        decimal? oldBuyRate,
        decimal? oldSellRate,
        decimal? newBuyRate,
        decimal? newSellRate,
        bool? oldIsManualOverride,
        bool? newIsManualOverride,
        DateTime? oldManualExpiresAt,
        DateTime? newManualExpiresAt,
        string note)
    {
        _dbContext.CurrencyRateSettingAudits.Add(new CurrencyRateSettingAudit
        {
            CurrencyRateSetting = setting,
            Action = action,
            OldBuyRate = oldBuyRate,
            OldSellRate = oldSellRate,
            NewBuyRate = newBuyRate,
            NewSellRate = newSellRate,
            OldIsManualOverride = oldIsManualOverride,
            NewIsManualOverride = newIsManualOverride,
            OldManualExpiresAt = oldManualExpiresAt,
            NewManualExpiresAt = newManualExpiresAt,
            ChangedBy = changedBy,
            ChangedAt = MongoliaClock.Now,
            Note = note
        });
    }

    private static TimeSpan? BuildManualOverrideDuration(int value, string unit)
    {
        if (value <= 0)
        {
            return null;
        }

        return unit.Trim().ToUpperInvariant() switch
        {
            "MINUTES" when value <= 1440 => TimeSpan.FromMinutes(value),
            "HOURS" when value <= 168 => TimeSpan.FromHours(value),
            "DAYS" when value <= 30 => TimeSpan.FromDays(value),
            _ => null
        };
    }

    private static decimal CalculateBuyRate(decimal baseRate, decimal marginPercent)
    {
        return NormalizeBuyRate(baseRate * (1m - marginPercent));
    }

    private static decimal CalculateSellRate(decimal baseRate, decimal marginPercent)
    {
        return NormalizeSellRate(baseRate * (1m + marginPercent));
    }

    private static decimal NormalizeBuyRate(decimal value)
    {
        return decimal.Floor(value);
    }

    private static decimal NormalizeSellRate(decimal value)
    {
        return decimal.Ceiling(value);
    }

    private IQueryable<SuspiciousTransactionDetail> QuerySuspiciousDetails()
    {
        return _dbContext.SuspiciousTransactionDetails
            .AsNoTracking()
            .Include(detail => detail.Transaction)
                .ThenInclude(transaction => transaction.FromAccount)
                    .ThenInclude(account => account.User)
            .Include(detail => detail.Transaction)
                .ThenInclude(transaction => transaction.ToAccount)
                    .ThenInclude(account => account.User)
            .Include(detail => detail.Transaction)
                .ThenInclude(transaction => transaction.TransactionDetectionLogs)
            .Include(detail => detail.ReviewedByNavigation);
    }

    private async Task<decimal> GetFraudDetectionThresholdAsync(CancellationToken cancellationToken)
    {
        var threshold = await _dbContext.FraudDetectionSettings
            .AsNoTracking()
            .Where(setting => setting.Id == 1)
            .Select(setting => (decimal?)setting.SuspiciousThreshold)
            .FirstOrDefaultAsync(cancellationToken);

        return threshold is > 0 ? threshold.Value : 60m;
    }

    private IQueryable<Transaction> BuildAiDetectionTransactionQuery(
        string? search,
        string? username,
        string? currency,
        decimal? minRiskScore,
        DateOnly? startDate,
        DateOnly? endDate)
    {
        IQueryable<Transaction> query = _dbContext.Transactions
            .AsNoTracking()
            .Include(transaction => transaction.FromAccount)
                .ThenInclude(account => account.User)
            .Include(transaction => transaction.ToAccount)
                .ThenInclude(account => account.User)
            .Include(transaction => transaction.TransactionDetectionLogs)
            .Include(transaction => transaction.AiTransactionAnalysisLogs);

        var normalizedSearch = NormalizeSearch(search);
        if (normalizedSearch is not null)
        {
            query = query.Where(transaction =>
                transaction.FromAccount.AccountNumber.Contains(normalizedSearch) ||
                transaction.ToAccount.AccountNumber.Contains(normalizedSearch) ||
                transaction.Status.Contains(normalizedSearch) ||
                (transaction.Description ?? "").Contains(normalizedSearch));
        }

        var normalizedUsername = NormalizeSearch(username);
        if (normalizedUsername is not null)
        {
            query = query.Where(transaction =>
                transaction.FromAccount.User.Username.Contains(normalizedUsername) ||
                transaction.ToAccount.User.Username.Contains(normalizedUsername) ||
                (transaction.FromAccount.User.FirstName ?? "").Contains(normalizedUsername) ||
                (transaction.FromAccount.User.LastName ?? "").Contains(normalizedUsername) ||
                (transaction.ToAccount.User.FirstName ?? "").Contains(normalizedUsername) ||
                (transaction.ToAccount.User.LastName ?? "").Contains(normalizedUsername));
        }

        var normalizedCurrency = currency?.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(normalizedCurrency))
        {
            query = query.Where(transaction =>
                transaction.SourceCurrency == normalizedCurrency ||
                transaction.TargetCurrency == normalizedCurrency);
        }

        if (minRiskScore is not null)
        {
            query = query.Where(transaction =>
                transaction.TransactionDetectionLogs
                    .OrderByDescending(log => log.CreatedAt)
                    .Select(log => log.RiskScore ?? -1m)
                    .FirstOrDefault() >= minRiskScore.Value);
        }

        var start = startDate?.ToDateTime(TimeOnly.MinValue);
        if (start is not null)
        {
            query = query.Where(transaction => transaction.CreatedAt >= start.Value);
        }

        var endExclusive = endDate?.AddDays(1).ToDateTime(TimeOnly.MinValue);
        if (endExclusive is not null)
        {
            query = query.Where(transaction => transaction.CreatedAt < endExclusive.Value);
        }

        return query;
    }

    private async Task<GeminiSuspiciousAnalysisContextDto?> BuildGeminiAnalysisContextAsync(
        long transactionId,
        CancellationToken cancellationToken)
    {
        var transaction = await _dbContext.Transactions
            .AsNoTracking()
            .Include(item => item.FromAccount)
            .Include(item => item.ToAccount)
            .Include(item => item.TransactionDetectionLogs)
            .Include(item => item.SuspiciousTransactionDetail)
            .FirstOrDefaultAsync(item => item.Id == transactionId, cancellationToken);

        if (transaction is null)
        {
            return null;
        }

        var latestDetection = transaction.TransactionDetectionLogs
            .OrderByDescending(log => log.CreatedAt)
            .FirstOrDefault();

        return new GeminiSuspiciousAnalysisContextDto
        {
            TransactionId = transaction.Id,
            CreatedAt = transaction.CreatedAt,
            Amount = transaction.Amount,
            SourceCurrency = transaction.SourceCurrency,
            CreditedAmount = transaction.CreditedAmount,
            TargetCurrency = transaction.TargetCurrency,
            RiskScore = latestDetection?.RiskScore ?? 0m,
            SuspiciousReason = latestDetection?.Reason ?? "Rule-based detection бүртгэл байхгүй.",
            ReviewStatus = transaction.SuspiciousTransactionDetail?.ReviewStatus ?? "NOT_REVIEWED",
            FromAccountMasked = MaskAccountNumber(transaction.FromAccount.AccountNumber),
            ToAccountMasked = MaskAccountNumber(transaction.ToAccount.AccountNumber),
            Description = transaction.Description,
            IsCrossCurrency = !string.Equals(transaction.SourceCurrency, transaction.TargetCurrency, StringComparison.OrdinalIgnoreCase),
            ExchangeRateValue = transaction.ExchangeRateValue,
            DetectionCheckedAt = transaction.DetectionCheckedAt
        };
    }

    private static string MaskAccountNumber(string accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
        {
            return "-";
        }

        var trimmed = accountNumber.Trim();
        if (trimmed.Length <= 4)
        {
            return new string('*', trimmed.Length);
        }

        return $"{trimmed[..2]}{new string('*', Math.Max(2, trimmed.Length - 4))}{trimmed[^2..]}";
    }

    private static AdminSuspiciousTransactionDto MapSuspiciousDetail(SuspiciousTransactionDetail detail)
    {
        var latestDetection = detail.Transaction.TransactionDetectionLogs
            .OrderByDescending(log => log.CreatedAt)
            .FirstOrDefault();

        return new AdminSuspiciousTransactionDto
        {
            IsDraft = false,
            TransactionId = detail.TransactionId,
            FromAccountId = detail.Transaction.FromAccountId,
            FromAccountNumber = detail.Transaction.FromAccount.AccountNumber,
            FromUserId = detail.Transaction.FromAccount.UserId,
            FromUserName = BuildUserDisplayName(detail.Transaction.FromAccount.User),
            ToAccountId = detail.Transaction.ToAccountId,
            ToAccountNumber = detail.Transaction.ToAccount.AccountNumber,
            ToUserId = detail.Transaction.ToAccount.UserId,
            ToUserName = BuildUserDisplayName(detail.Transaction.ToAccount.User),
            Amount = detail.Transaction.Amount,
            SourceCurrency = detail.Transaction.SourceCurrency,
            CreditedAmount = detail.Transaction.CreditedAmount,
            TargetCurrency = detail.Transaction.TargetCurrency,
            CreatedAt = detail.Transaction.CreatedAt,
            RiskScore = detail.RiskScore,
            SuspiciousReason = detail.SuspiciousReason,
            AiExplanation = BuildAiExplanation(detail),
            ReviewCreatedAt = detail.CreatedAt,
            DetectionLoggedAt = latestDetection?.CreatedAt,
            DetectionStatus = latestDetection?.ServiceStatus,
            DetectionSource = latestDetection?.Source,
            DetectionRiskScore = latestDetection?.RiskScore,
            DetectionReason = latestDetection?.Reason,
            DetectionTriggeredRules = latestDetection?.TriggeredRules,
            DetectionRules = ParseDetectionRules(latestDetection?.TriggeredRules),
            ReviewStatus = detail.ReviewStatus,
            ReviewStatusLabel = ReviewStatusHelper.GetLabel(detail.ReviewStatus),
            ReviewNote = detail.ReviewNote,
            ReviewedBy = detail.ReviewedBy,
            ReviewedByUsername = detail.ReviewedByNavigation?.Username,
            ReviewedAt = detail.ReviewedAt,
            UpdatedAt = detail.UpdatedAt
        };
    }

    private static AdminSuspiciousTransactionDto MapSuspiciousReviewDraft(Transaction transaction)
    {
        var latestDetection = transaction.TransactionDetectionLogs
            .OrderByDescending(log => log.CreatedAt)
            .FirstOrDefault();
        var latestAi = transaction.AiTransactionAnalysisLogs
            .OrderByDescending(log => log.CreatedAt)
            .FirstOrDefault();
        var riskScore = latestAi?.RiskScore ?? latestDetection?.RiskScore ?? 0m;
        var reason = latestAi?.Explanation ?? latestDetection?.Reason ?? "Review хийх автомат шалтгаан бүртгэгдээгүй.";

        return new AdminSuspiciousTransactionDto
        {
            IsDraft = true,
            TransactionId = transaction.Id,
            FromAccountId = transaction.FromAccountId,
            FromAccountNumber = transaction.FromAccount.AccountNumber,
            FromUserId = transaction.FromAccount.UserId,
            FromUserName = BuildUserDisplayName(transaction.FromAccount.User),
            ToAccountId = transaction.ToAccountId,
            ToAccountNumber = transaction.ToAccount.AccountNumber,
            ToUserId = transaction.ToAccount.UserId,
            ToUserName = BuildUserDisplayName(transaction.ToAccount.User),
            Amount = transaction.Amount,
            SourceCurrency = transaction.SourceCurrency,
            CreditedAmount = transaction.CreditedAmount,
            TargetCurrency = transaction.TargetCurrency,
            CreatedAt = transaction.CreatedAt,
            RiskScore = riskScore,
            SuspiciousReason = reason,
            AiExplanation = latestAi?.Explanation ?? reason,
            ReviewCreatedAt = transaction.CreatedAt,
            DetectionLoggedAt = latestDetection?.CreatedAt,
            DetectionStatus = latestDetection?.ServiceStatus,
            DetectionSource = latestDetection?.Source,
            DetectionRiskScore = latestDetection?.RiskScore,
            DetectionReason = latestDetection?.Reason,
            DetectionTriggeredRules = latestDetection?.TriggeredRules,
            DetectionRules = ParseDetectionRules(latestDetection?.TriggeredRules),
            ReviewStatus = "REVIEWING",
            ReviewStatusLabel = "Хадгалагдаагүй төлөвлөгөө",
            UpdatedAt = DateTime.MinValue
        };
    }

    private static string BuildAiExplanation(SuspiciousTransactionDetail detail)
    {
        if (!string.IsNullOrWhiteSpace(detail.AiExplanation))
        {
            return detail.AiExplanation;
        }

        return $"Энэ гүйлгээ rule-based шалгалтаар сэжигтэй гэж тэмдэглэгдсэн байна. Дэлгэрэнгүй шалтгаан: {detail.SuspiciousReason}";
    }

    private static IReadOnlyList<string> ParseDetectionRules(string? triggeredRules)
    {
        if (string.IsNullOrWhiteSpace(triggeredRules))
        {
            return Array.Empty<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(triggeredRules) ?? [];
        }
        catch (JsonException)
        {
            return triggeredRules
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(rule => rule.Trim('[', ']', '"'))
                .Where(rule => !string.IsNullOrWhiteSpace(rule))
                .ToList();
        }
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

    private void DeactivateSuspiciousAccount(long adminUserId, Account account, long transactionId, string action)
    {
        if (!account.IsActive && account.IsAdminLocked)
        {
            return;
        }

        var oldValue = new
        {
            account.IsActive,
            account.IsAdminLocked,
            account.AdminLockedAt,
            account.AdminLockedByUserId
        };
        var now = MongoliaClock.Now;
        account.IsActive = false;
        account.IsAdminLocked = true;
        account.AdminLockedAt = now;
        account.AdminLockedByUserId = adminUserId;
        account.UpdatedAt = now;

        AddAuditLog(
            adminUserId,
            action,
            "accounts",
            account.Id,
            oldValue,
            new
            {
                account.IsActive,
                account.IsAdminLocked,
                account.AdminLockedAt,
                account.AdminLockedByUserId,
                transactionId
            },
            $"Account {account.AccountNumber} deactivated from suspicious transaction #{transactionId} workflow.");

        _dbContext.Notifications.Add(new Notification
        {
            UserId = account.UserId,
            TransactionId = transactionId,
            NotificationType = "ACCOUNT_STATUS_UPDATED",
            Title = "Дансны төлөв өөрчлөгдлөө",
            Message = $"Таны {account.AccountNumber} данс аюулгүй байдлын шалгалтын хүрээнд түр идэвхгүй боллоо. Дэлгэрэнгүй мэдээлэл авах бол банкны ажилтантай холбогдоно уу.",
            IsRead = false,
            CreatedAt = account.UpdatedAt
        });
    }

    private void DeactivateSuspiciousUser(long adminUserId, User user, long transactionId, string action)
    {
        if (!user.IsActive)
        {
            return;
        }

        var oldValue = new { user.IsActive };
        user.IsActive = false;
        user.UpdatedAt = MongoliaClock.Now;

        AddAuditLog(
            adminUserId,
            action,
            "users",
            user.Id,
            oldValue,
            new { user.IsActive, transactionId },
            $"User {user.Username} deactivated from suspicious transaction #{transactionId} workflow.");
    }

    private static Notification BuildFraudActionNotification(
        long userId,
        long transactionId,
        string title,
        string? customMessage,
        string defaultMessage)
    {
        return new Notification
        {
            UserId = userId,
            TransactionId = transactionId,
            NotificationType = "SECURITY_REVIEW_UPDATE",
            Title = title,
            Message = string.IsNullOrWhiteSpace(customMessage) ? defaultMessage : customMessage.Trim(),
            IsRead = false,
            CreatedAt = MongoliaClock.Now
        };
    }

    private static Notification? BuildReviewNotification(SuspiciousTransactionDetail detail, string? customMessage)
    {
        var defaultMessage = detail.ReviewStatus switch
        {
            "CONFIRMED" => "Таны нэг гүйлгээ аюулгүй байдлын нэмэлт шалгалтаар сэжигтэй гэж баталгаажлаа. Дэлгэрэнгүй мэдээлэл шаардлагатай бол банкны ажилтантай холбогдоно уу.",
            "REVIEWING" => "Таны нэг гүйлгээ аюулгүй байдлын нэмэлт шалгалтад орсон байна. Шалгалт дуусах хүртэл банкнаас ирэх зааврыг дагана уу.",
            "RESOLVED" => "Таны гүйлгээний нэмэлт шалгалт шийдвэрлэгдлээ.",
            "FALSE_ALARM" => "Таны гүйлгээний нэмэлт шалгалт дууслаа. Сэжигтэй асуудал илрээгүй.",
            _ => null
        };

        var message = string.IsNullOrWhiteSpace(customMessage)
            ? defaultMessage
            : customMessage.Trim();

        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        return new Notification
        {
            UserId = detail.Transaction.FromAccount.UserId,
            TransactionId = detail.TransactionId,
            NotificationType = detail.ReviewStatus == "CONFIRMED" ? "SECURITY_REVIEW" : "SECURITY_REVIEW_UPDATE",
            Title = detail.ReviewStatus == "CONFIRMED" ? "Гүйлгээний анхааруулга" : "Гүйлгээний шалгалтын мэдээлэл",
            Message = message,
            IsRead = false,
            CreatedAt = MongoliaClock.Now
        };
    }
    private static string? BuildFullName(string? firstName, string? lastName)
    {
        return UserDisplayNameFormatter.FormatOrNull(firstName, lastName);
    }
}
