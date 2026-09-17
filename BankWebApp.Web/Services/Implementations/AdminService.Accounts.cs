using BankWebApp.Web.Data.Entities;
using BankWebApp.Web.DTOs.Admin;
using BankWebApp.Web.Helpers;
using Microsoft.EntityFrameworkCore;

namespace BankWebApp.Web.Services.Implementations;

// AdminService-ийн Accounts үүрэгтэй хэсэг; тусдаа instance үүсгэхгүй.
public partial class AdminService
{
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
}
