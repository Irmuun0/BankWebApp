using System.Text.Json;
using BankWebApp.Web.Components.Charts;
using BankWebApp.Web.Data.Entities;
using BankWebApp.Web.DTOs.Admin;
using BankWebApp.Web.Helpers;
using Microsoft.EntityFrameworkCore;

namespace BankWebApp.Web.Services.Implementations;

// AdminService-ийн Reports үүрэгтэй хэсэг; тусдаа instance үүсгэхгүй.
public partial class AdminService
{
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
}
