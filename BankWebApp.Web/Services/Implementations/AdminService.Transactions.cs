using BankWebApp.Web.Data.Entities;
using BankWebApp.Web.DTOs.Admin;
using Microsoft.EntityFrameworkCore;

namespace BankWebApp.Web.Services.Implementations;

// AdminService-ийн Transactions үүрэгтэй хэсэг; тусдаа instance үүсгэхгүй.
public partial class AdminService
{
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
}
