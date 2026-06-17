using System.Text.Json;
using BankWebApp.Web.Data;
using BankWebApp.Web.Data.Entities;
using BankWebApp.Web.DTOs.Admin;
using BankWebApp.Web.Helpers;
using BankWebApp.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BankWebApp.Web.Services.Implementations;

public class AdminService : IAdminService
{
    private readonly BankDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AdminService(BankDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
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
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Users.AsNoTracking();
        var normalizedSearch = NormalizeSearch(search);
        if (normalizedSearch is not null)
        {
            query = query.Where(user =>
                user.Username.Contains(normalizedSearch) ||
                user.Email.Contains(normalizedSearch) ||
                user.PhoneNumber.Contains(normalizedSearch) ||
                ((user.FirstName ?? "") + " " + (user.LastName ?? "")).Contains(normalizedSearch));
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
                FullName = ((user.FirstName ?? "") + " " + (user.LastName ?? "")).Trim(),
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

    public async Task<AdminPagedResultDto<AdminAccountDto>> GetAccountsAsync(
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Account> query = _dbContext.Accounts
            .AsNoTracking()
            .Include(account => account.User);
        var normalizedSearch = NormalizeSearch(search);
        if (normalizedSearch is not null)
        {
            query = query.Where(account =>
                account.AccountNumber.Contains(normalizedSearch) ||
                account.Currency.Contains(normalizedSearch) ||
                account.User.Username.Contains(normalizedSearch) ||
                ((account.User.FirstName ?? "") + " " + (account.User.LastName ?? "")).Contains(normalizedSearch));
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
                FullName = ((account.User.FirstName ?? "") + " " + (account.User.LastName ?? "")).Trim(),
                AccountNumber = account.AccountNumber,
                AccountType = account.AccountType,
                Currency = account.Currency,
                Balance = account.Balance,
                IsActive = account.IsActive,
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

    public async Task<AdminPagedResultDto<AdminTransactionDto>> GetTransactionsAsync(
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Transaction> query = _dbContext.Transactions
            .AsNoTracking()
            .Include(transaction => transaction.FromAccount)
            .Include(transaction => transaction.ToAccount)
            .Include(transaction => transaction.TransactionDetectionLogs);
        var normalizedSearch = NormalizeSearch(search);
        if (normalizedSearch is not null)
        {
            query = query.Where(transaction =>
                transaction.FromAccount.AccountNumber.Contains(normalizedSearch) ||
                transaction.ToAccount.AccountNumber.Contains(normalizedSearch) ||
                transaction.Status.Contains(normalizedSearch) ||
                (transaction.Description ?? "").Contains(normalizedSearch));
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

    public async Task<AdminPagedResultDto<AdminAuditLogDto>> GetAuditLogsAsync(
        string? source = null,
        string? search = null,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var normalizedSource = NormalizeAuditSource(source);
        var normalizedSearch = NormalizeSearch(search);
        var start = startDate?.ToDateTime(TimeOnly.MinValue);
        var endExclusive = endDate?.AddDays(1).ToDateTime(TimeOnly.MinValue);
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
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var today = MongoliaClock.Today;
        var normalizedStartDate = startDate ?? new DateOnly(today.Year, today.Month, 1);
        var normalizedEndDate = endDate ?? today;

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

        var normalizedSearch = NormalizeSearch(search);
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

        var totalItems = await query.CountAsync(cancellationToken);
        var totalIncome = await query.SumAsync(log => (decimal?)log.IncomeAmountMnt, cancellationToken) ?? 0m;
        var buyIncome = await query
            .Where(log => log.IncomeType == "BUY_SPREAD")
            .SumAsync(log => (decimal?)log.IncomeAmountMnt, cancellationToken) ?? 0m;
        var sellIncome = await query
            .Where(log => log.IncomeType == "SELL_SPREAD")
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
        string? search = null,
        string? reviewStatus = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = QuerySuspiciousDetails();
        var normalizedSearch = NormalizeSearch(search);
        if (normalizedSearch is not null)
        {
            query = query.Where(detail =>
                detail.Transaction.FromAccount.AccountNumber.Contains(normalizedSearch) ||
                detail.Transaction.ToAccount.AccountNumber.Contains(normalizedSearch) ||
                detail.SuspiciousReason.Contains(normalizedSearch) ||
                (detail.ReviewNote ?? "").Contains(normalizedSearch));
        }

        if (ReviewStatusHelper.IsValid(reviewStatus))
        {
            var normalizedStatus = ReviewStatusHelper.Normalize(reviewStatus!);
            query = query.Where(detail => detail.ReviewStatus == normalizedStatus);
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

        return detail is null ? null : MapSuspiciousDetail(detail);
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
            .FirstOrDefaultAsync(item => item.TransactionId == dto.TransactionId, cancellationToken);

        if (detail is null)
        {
            return (false, "Сэжигтэй гүйлгээ олдсонгүй.");
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

        AddAuditLog(
            adminUserId,
            "SUSPICIOUS_REVIEW_UPDATED",
            "suspicious_transaction_details",
            detail.Id,
            oldValue,
            new
            {
                detail.ReviewStatus,
                detail.ReviewNote,
                detail.ReviewedBy,
                detail.ReviewedAt
            },
            $"Transaction #{detail.TransactionId} review updated.");

        var notification = BuildReviewNotification(detail);
        if (notification is not null)
        {
            _dbContext.Notifications.Add(notification);
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
            $"User {user.Username} active status updated.");

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

        var oldValue = new { account.IsActive };
        account.IsActive = isActive;
        account.UpdatedAt = MongoliaClock.Now;

        AddAuditLog(
            adminUserId,
            "ACCOUNT_STATUS_UPDATED",
            "accounts",
            account.Id,
            oldValue,
            new { account.IsActive },
            $"Account {account.AccountNumber} active status updated.");

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
            ActionLabel = HumanizeAction(log.Action),
            TargetType = log.TargetType,
            TargetId = log.TargetId,
            Summary = log.Detail ?? $"{HumanizeAction(log.Action)} {log.TargetType} #{log.TargetId}",
            Detail = log.Detail,
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
            var fullName = $"{user.FirstName} {user.LastName}".Trim();
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

    private static string HumanizeAction(string value)
    {
        return value.Replace('_', ' ').ToLowerInvariant();
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
            .Include(detail => detail.Transaction)
                .ThenInclude(transaction => transaction.ToAccount)
            .Include(detail => detail.ReviewedByNavigation);
    }

    private static AdminSuspiciousTransactionDto MapSuspiciousDetail(SuspiciousTransactionDetail detail)
    {
        return new AdminSuspiciousTransactionDto
        {
            TransactionId = detail.TransactionId,
            FromAccountNumber = detail.Transaction.FromAccount.AccountNumber,
            ToAccountNumber = detail.Transaction.ToAccount.AccountNumber,
            Amount = detail.Transaction.Amount,
            SourceCurrency = detail.Transaction.SourceCurrency,
            CreditedAmount = detail.Transaction.CreditedAmount,
            TargetCurrency = detail.Transaction.TargetCurrency,
            CreatedAt = detail.Transaction.CreatedAt,
            RiskScore = detail.RiskScore,
            SuspiciousReason = detail.SuspiciousReason,
            AiExplanation = BuildAiExplanation(detail),
            ReviewStatus = detail.ReviewStatus,
            ReviewStatusLabel = ReviewStatusHelper.GetLabel(detail.ReviewStatus),
            ReviewNote = detail.ReviewNote,
            ReviewedBy = detail.ReviewedBy,
            ReviewedByUsername = detail.ReviewedByNavigation?.Username,
            ReviewedAt = detail.ReviewedAt,
            UpdatedAt = detail.UpdatedAt
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

    private static (int Page, int PageSize) NormalizePage(int page, int pageSize, int totalItems)
    {
        var normalizedPageSize = Math.Clamp(pageSize, 5, 100);
        var totalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)normalizedPageSize);
        var normalizedPage = Math.Clamp(page, 1, totalPages);
        return (normalizedPage, normalizedPageSize);
    }

    private static Notification? BuildReviewNotification(SuspiciousTransactionDetail detail)
    {
        var message = detail.ReviewStatus switch
        {
            "CONFIRMED" => "Таны гүйлгээ аюулгүй байдлын нэмэлт шалгалтаар баталгаажлаа. Шаардлагатай бол банкны ажилтан тантай холбогдоно.",
            "FALSE_ALARM" => "Таны гүйлгээний нэмэлт шалгалт дууслаа. Асуудал илрээгүй.",
            "RESOLVED" => "Таны гүйлгээний нэмэлт шалгалт шийдвэрлэгдлээ.",
            _ => null
        };

        if (message is null)
        {
            return null;
        }

        return new Notification
        {
            UserId = detail.Transaction.FromAccount.UserId,
            TransactionId = detail.TransactionId,
            NotificationType = "SECURITY_REVIEW_UPDATE",
            Title = "Гүйлгээний шалгалтын мэдээлэл",
            Message = message,
            IsRead = false,
            CreatedAt = MongoliaClock.Now
        };
    }

    private static string? BuildFullName(string? firstName, string? lastName)
    {
        var fullName = $"{firstName} {lastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? null : fullName;
    }
}
