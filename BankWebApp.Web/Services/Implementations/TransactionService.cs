using System.Text.Json;
using BankWebApp.Web.Data;
using BankWebApp.Web.Data.Entities;
using BankWebApp.Web.DTOs.Ai;
using BankWebApp.Web.DTOs.Transactions;
using BankWebApp.Web.Helpers;
using BankWebApp.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BankWebApp.Web.Services.Implementations;

public class TransactionService : ITransactionService
{
    private readonly BankDbContext _dbContext;
    private readonly IExchangeRateService _exchangeRateService;
    private readonly IAiDetectionService _aiDetectionService;
    private readonly ILogger<TransactionService> _logger;

    public TransactionService(
        BankDbContext dbContext,
        IExchangeRateService exchangeRateService,
        IAiDetectionService aiDetectionService,
        ILogger<TransactionService> logger)
    {
        _dbContext = dbContext;
        _exchangeRateService = exchangeRateService;
        _aiDetectionService = aiDetectionService;
        _logger = logger;
    }

    public async Task<List<UserTransactionDto>> GetMyTransactionsAsync(long currentUserId, CancellationToken cancellationToken = default)
    {
        return await BuildMyTransactionsQuery(currentUserId, null)
            .Take(100)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<UserTransactionDto>> GetMyTransactionsAsync(
        long currentUserId,
        DateOnly? startDate,
        DateOnly? endDate,
        int count = 500,
        CancellationToken cancellationToken = default)
    {
        var query = BuildMyTransactionsQuery(currentUserId, null);

        if (startDate is not null)
        {
            var start = startDate.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(transaction => transaction.CreatedAt >= start);
        }

        if (endDate is not null)
        {
            var endExclusive = endDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(transaction => transaction.CreatedAt < endExclusive);
        }

        return await query
            .Take(Math.Clamp(count, 1, 500))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<UserTransactionDto>> GetMyTransactionsAsync(
        long currentUserId,
        long accountId,
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default)
    {
        var query = BuildMyTransactionsQuery(currentUserId, accountId);

        if (startDate is not null)
        {
            var start = startDate.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(transaction => transaction.CreatedAt >= start);
        }

        if (endDate is not null)
        {
            var endExclusive = endDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(transaction => transaction.CreatedAt < endExclusive);
        }

        return await query
            .Take(500)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<UserTransactionDto>> GetRecentMyTransactionsAsync(long currentUserId, int count = 5, CancellationToken cancellationToken = default)
    {
        return await BuildMyTransactionsQuery(currentUserId, null)
            .Take(Math.Clamp(count, 1, 20))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<UserTransactionDto>> GetRecentMyTransactionsAsync(long currentUserId, long accountId, int count = 5, CancellationToken cancellationToken = default)
    {
        return await BuildMyTransactionsQuery(currentUserId, accountId)
            .Take(Math.Clamp(count, 1, 20))
            .ToListAsync(cancellationToken);
    }

    public async Task<(bool Success, string? ErrorMessage, long? TransactionId)> CreateTransactionAsync(
        long currentUserId,
        CreateTransactionDto dto,
        CancellationToken cancellationToken = default)
    {
        var receiverAccountNumber = dto.ToAccountNumber.Trim();
        var amount = decimal.Round(dto.Amount, 2, MidpointRounding.AwayFromZero);
        var description = dto.Description?.Trim();

        if (dto.FromAccountId <= 0)
        {
            return Failed("Илгээх дансаа сонгоно уу.");
        }

        if (string.IsNullOrWhiteSpace(receiverAccountNumber))
        {
            return Failed("Хүлээн авах дансны дугаараа оруулна уу.");
        }

        if (amount <= 0)
        {
            return Failed("Гүйлгээний дүн 0-ээс их байх ёстой.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return Failed("Гүйлгээний утга оруулна уу.");
        }

        await using var dbTransaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

        try
        {
            var fromAccount = await _dbContext.Accounts
                .FirstOrDefaultAsync(account =>
                    account.Id == dto.FromAccountId &&
                    account.UserId == currentUserId &&
                    account.IsActive,
                    cancellationToken);

            if (fromAccount is null)
            {
                await dbTransaction.RollbackAsync(cancellationToken);
                return Failed("Илгээх идэвхтэй данс олдсонгүй.");
            }

            var debitAmountMnt = await ConvertDebitAmountToMntAsync(fromAccount.Currency, amount, cancellationToken);
            if (!debitAmountMnt.Success)
            {
                await dbTransaction.RollbackAsync(cancellationToken);
                return Failed(debitAmountMnt.ErrorMessage ?? "Өдрийн лимит шалгах ханш олдсонгүй.");
            }

            var todayDebitTotalMnt = await GetTodayDebitTotalMntAsync(fromAccount.Id, fromAccount.Currency, cancellationToken);
            var projectedDailyTotalMnt = todayDebitTotalMnt + debitAmountMnt.AmountMnt;
            if (projectedDailyTotalMnt > fromAccount.DailyTransactionLimitMnt)
            {
                await dbTransaction.RollbackAsync(cancellationToken);
                var remainingLimit = Math.Max(0m, fromAccount.DailyTransactionLimitMnt - todayDebitTotalMnt);
                return Failed($"Энэ дансны өдрийн гүйлгээний лимит {fromAccount.DailyTransactionLimitMnt:N2} MNT. Өнөөдөр ашигласан: {todayDebitTotalMnt:N2} MNT. Үлдсэн лимит: {remainingLimit:N2} MNT.");
            }

            var toAccount = await _dbContext.Accounts
                .FirstOrDefaultAsync(account => account.AccountNumber == receiverAccountNumber, cancellationToken);

            if (toAccount is null)
            {
                await dbTransaction.RollbackAsync(cancellationToken);
                return Failed("Хүлээн авах дансны дугаар буруу байна.");
            }

            if (!toAccount.IsActive)
            {
                await dbTransaction.RollbackAsync(cancellationToken);
                return Failed("Хүлээн авах данс идэвхгүй байна.");
            }

            if (fromAccount.Id == toAccount.Id)
            {
                await dbTransaction.RollbackAsync(cancellationToken);
                return Failed("Нэг данс руу өөрөөс нь гүйлгээ хийх боломжгүй.");
            }

            if (fromAccount.Balance < amount)
            {
                await dbTransaction.RollbackAsync(cancellationToken);
                return Failed("Дансны үлдэгдэл хүрэлцэхгүй байна.");
            }

            var conversion = await CalculateCreditedAmountAsync(fromAccount.Currency, toAccount.Currency, amount, cancellationToken);
            if (!conversion.Success)
            {
                await dbTransaction.RollbackAsync(cancellationToken);
                return Failed(conversion.ErrorMessage ?? "Валютын ханш олдсонгүй.");
            }

            if (!string.Equals(fromAccount.Currency, toAccount.Currency, StringComparison.OrdinalIgnoreCase) &&
                conversion.CreditedAmount < 1m)
            {
                await dbTransaction.RollbackAsync(cancellationToken);
                return Failed("Хүлээн авах дүн 1-ээс их байх ёстой.");
            }

            var now = MongoliaClock.Now;
            fromAccount.Balance -= amount;
            fromAccount.UpdatedAt = now;
            toAccount.Balance += conversion.CreditedAmount;
            toAccount.UpdatedAt = now;

            var transaction = new Transaction
            {
                FromAccountId = fromAccount.Id,
                ToAccountId = toAccount.Id,
                Amount = amount,
                SourceCurrency = fromAccount.Currency,
                CreditedAmount = conversion.CreditedAmount,
                TargetCurrency = toAccount.Currency,
                ExchangeRateLogId = conversion.ExchangeRateLogId,
                ExchangeRateValue = conversion.ExchangeRateValue,
                RoundingDifference = conversion.RoundingDifference,
                Description = description,
                Status = "SUCCESS",
                FailureReason = null,
                IsSuspicious = false,
                DetectionCheckedAt = null,
                CreatedAt = now
            };

            _dbContext.Transactions.Add(transaction);
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (conversion.FxIncome is not null)
            {
                conversion.FxIncome.TransactionId = transaction.Id;
                conversion.FxIncome.CreatedAt = now;
                _dbContext.FxIncomeLogs.Add(conversion.FxIncome);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            if (toAccount.UserId != currentUserId)
            {
                _dbContext.Notifications.Add(new Notification
                {
                    UserId = toAccount.UserId,
                    TransactionId = transaction.Id,
                    NotificationType = "TRANSACTION_SUCCESS",
                    Title = "Мөнгө орж ирлээ",
                    Message = $"{transaction.CreditedAmount:N2} {transaction.TargetCurrency} таны {toAccount.AccountNumber} дансанд орлоо.",
                    IsRead = false,
                    CreatedAt = now
                });
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            await dbTransaction.CommitAsync(cancellationToken);

            await ProcessSuspiciousDetectionAsync(currentUserId, transaction.Id, cancellationToken);

            return (true, null, transaction.Id);
        }
        catch (Exception)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            return Failed("Гүйлгээ хийх үед алдаа гарлаа. Дансны үлдэгдэл өөрчлөгдөөгүй.");
        }
    }

    private async Task ProcessSuspiciousDetectionAsync(
        long currentUserId,
        long transactionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = await BuildSuspiciousDetectionRequestAsync(currentUserId, transactionId, cancellationToken);
            if (request is null)
            {
                return;
            }

            var result = await _aiDetectionService.DetectSuspiciousAsync(request, cancellationToken);
            if (result is null)
            {
                await SaveDetectionUnavailableLogAsync(transactionId, cancellationToken);
                return;
            }

            var now = MongoliaClock.Now;

            await using (var detectionTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken))
            {
                var transaction = await _dbContext.Transactions
                    .Include(item => item.FromAccount)
                    .FirstOrDefaultAsync(item => item.Id == transactionId, cancellationToken);

                if (transaction is null)
                {
                    await detectionTransaction.RollbackAsync(cancellationToken);
                    return;
                }

                transaction.DetectionCheckedAt = now;

                _dbContext.TransactionDetectionLogs.Add(new TransactionDetectionLog
                {
                    TransactionId = transactionId,
                    ServiceStatus = "CHECKED",
                    IsSuspicious = result.IsSuspicious,
                    RiskScore = Math.Clamp(result.RiskScore, 0m, 100m),
                    Reason = result.Reason,
                    TriggeredRules = JsonSerializer.Serialize(result.TriggeredRules),
                    Source = "FASTAPI_RULES",
                    CreatedAt = now
                });

                if (result.IsSuspicious)
                {
                    transaction.IsSuspicious = true;

                    var detailExists = await _dbContext.SuspiciousTransactionDetails
                        .AnyAsync(detail => detail.TransactionId == transactionId, cancellationToken);

                    if (!detailExists)
                    {
                        _dbContext.SuspiciousTransactionDetails.Add(new SuspiciousTransactionDetail
                        {
                            TransactionId = transactionId,
                            RiskScore = Math.Clamp(result.RiskScore, 0m, 100m),
                            SuspiciousReason = BuildSuspiciousReason(result),
                            AiExplanation = null,
                            ReviewStatus = "PENDING",
                            ReviewNote = null,
                            ReviewedBy = null,
                            ReviewedAt = null,
                            CreatedAt = now,
                            UpdatedAt = now
                        });
                    }
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                await detectionTransaction.CommitAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Suspicious detection post-processing failed for transaction {TransactionId}.", transactionId);
        }
    }

    private async Task SaveDetectionUnavailableLogAsync(long transactionId, CancellationToken cancellationToken)
    {
        try
        {
            _dbContext.TransactionDetectionLogs.Add(new TransactionDetectionLog
            {
                TransactionId = transactionId,
                ServiceStatus = "UNAVAILABLE",
                IsSuspicious = null,
                RiskScore = null,
                Reason = "FastAPI detection service unavailable or returned an invalid response.",
                TriggeredRules = null,
                Source = "FASTAPI_RULES",
                CreatedAt = MongoliaClock.Now
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save unavailable detection log for transaction {TransactionId}.", transactionId);
        }
    }

    private async Task<SuspiciousDetectionRequestDto?> BuildSuspiciousDetectionRequestAsync(
        long currentUserId,
        long transactionId,
        CancellationToken cancellationToken)
    {
        var transaction = await _dbContext.Transactions
            .AsNoTracking()
            .Include(item => item.FromAccount)
            .FirstOrDefaultAsync(item => item.Id == transactionId && item.FromAccount.UserId == currentUserId, cancellationToken);

        if (transaction is null)
        {
            return null;
        }

        var last30DaysStart = transaction.CreatedAt.AddDays(-30);
        var last24HoursStart = transaction.CreatedAt.AddHours(-24);
        var last30MinutesStart = transaction.CreatedAt.AddMinutes(-30);

        var sentTransactionsLast30Days = _dbContext.Transactions
            .AsNoTracking()
            .Where(item =>
                item.Id != transaction.Id &&
                item.FromAccount.UserId == currentUserId &&
                item.SourceCurrency == transaction.SourceCurrency &&
                item.CreatedAt >= last30DaysStart &&
                item.CreatedAt < transaction.CreatedAt);

        var averageAmount = await sentTransactionsLast30Days
            .AverageAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

        var maxAmount = await sentTransactionsLast30Days
            .MaxAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

        var sentCountLast24Hours = await _dbContext.Transactions
            .AsNoTracking()
            .CountAsync(item =>
                item.Id != transaction.Id &&
                item.FromAccount.UserId == currentUserId &&
                item.CreatedAt >= last24HoursStart &&
                item.CreatedAt < transaction.CreatedAt,
                cancellationToken);

        var sentTransactionsLast24Hours = _dbContext.Transactions
            .AsNoTracking()
            .Where(item =>
                item.Id != transaction.Id &&
                item.FromAccount.UserId == currentUserId &&
                item.SourceCurrency == transaction.SourceCurrency &&
                item.CreatedAt >= last24HoursStart &&
                item.CreatedAt < transaction.CreatedAt);

        var smallAmountThreshold = GetSmallAmountThreshold(transaction.SourceCurrency);
        var smallTransactionCountLast24Hours = await sentTransactionsLast24Hours
            .CountAsync(item => item.Amount <= smallAmountThreshold, cancellationToken);

        var smallTransactionTotalLast24Hours = await sentTransactionsLast24Hours
            .Where(item => item.Amount <= smallAmountThreshold)
            .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;

        if (transaction.Amount <= smallAmountThreshold)
        {
            smallTransactionCountLast24Hours += 1;
            smallTransactionTotalLast24Hours += transaction.Amount;
        }

        var distinctReceiverCountLast24Hours = await _dbContext.Transactions
            .AsNoTracking()
            .Where(item =>
                item.Id != transaction.Id &&
                item.FromAccount.UserId == currentUserId &&
                item.CreatedAt >= last24HoursStart &&
                item.CreatedAt < transaction.CreatedAt)
            .Select(item => item.ToAccountId)
            .Distinct()
            .CountAsync(cancellationToken);

        var receiverAlreadyCounted = await _dbContext.Transactions
            .AsNoTracking()
            .AnyAsync(item =>
                item.Id != transaction.Id &&
                item.FromAccount.UserId == currentUserId &&
                item.ToAccountId == transaction.ToAccountId &&
                item.CreatedAt >= last24HoursStart &&
                item.CreatedAt < transaction.CreatedAt,
                cancellationToken);

        if (!receiverAlreadyCounted)
        {
            distinctReceiverCountLast24Hours += 1;
        }

        var distinctSenderCountToReceiverLast24Hours = await _dbContext.Transactions
            .AsNoTracking()
            .Where(item =>
                item.Id != transaction.Id &&
                item.ToAccountId == transaction.ToAccountId &&
                item.CreatedAt >= last24HoursStart &&
                item.CreatedAt < transaction.CreatedAt)
            .Select(item => item.FromAccount.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        var senderAlreadyCountedForReceiver = await _dbContext.Transactions
            .AsNoTracking()
            .AnyAsync(item =>
                item.Id != transaction.Id &&
                item.ToAccountId == transaction.ToAccountId &&
                item.FromAccount.UserId == currentUserId &&
                item.CreatedAt >= last24HoursStart &&
                item.CreatedAt < transaction.CreatedAt,
                cancellationToken);

        if (!senderAlreadyCountedForReceiver)
        {
            distinctSenderCountToReceiverLast24Hours += 1;
        }

        var recentInboundAmountLast30Minutes = await _dbContext.Transactions
            .AsNoTracking()
            .Where(item =>
                item.Id != transaction.Id &&
                item.ToAccountId == transaction.FromAccountId &&
                item.TargetCurrency == transaction.SourceCurrency &&
                item.CreatedAt >= last30MinutesStart &&
                item.CreatedAt < transaction.CreatedAt)
            .SumAsync(item => (decimal?)item.CreditedAmount, cancellationToken) ?? 0m;

        var senderAccountAgeDays = Math.Max(0, (transaction.CreatedAt.Date - transaction.FromAccount.CreatedAt.Date).Days);

        var lastPreviousTransactionAt = await _dbContext.Transactions
            .AsNoTracking()
            .Where(item =>
                item.Id != transaction.Id &&
                (item.FromAccountId == transaction.FromAccountId || item.ToAccountId == transaction.FromAccountId) &&
                item.CreatedAt < transaction.CreatedAt)
            .MaxAsync(item => (DateTime?)item.CreatedAt, cancellationToken);

        int? senderDaysSinceLastTransaction = lastPreviousTransactionAt is null
            ? null
            : Math.Max(0, (transaction.CreatedAt.Date - lastPreviousTransactionAt.Value.Date).Days);

        var detectionSettings = await BuildDetectionSettingsAsync(cancellationToken);

        return new SuspiciousDetectionRequestDto
        {
            TransactionId = transaction.Id,
            SenderUserId = currentUserId,
            Amount = transaction.Amount,
            SourceCurrency = transaction.SourceCurrency,
            CreditedAmount = transaction.CreditedAmount,
            TargetCurrency = transaction.TargetCurrency,
            IsCrossCurrency = !string.Equals(transaction.SourceCurrency, transaction.TargetCurrency, StringComparison.OrdinalIgnoreCase),
            Description = transaction.Description,
            CreatedHour = transaction.CreatedAt.Hour,
            SenderAverageAmountLast30Days = decimal.Round(averageAmount, 2, MidpointRounding.AwayFromZero),
            SenderMaxAmountLast30Days = decimal.Round(maxAmount, 2, MidpointRounding.AwayFromZero),
            SenderTransactionCountLast24Hours = sentCountLast24Hours,
            SmallTransactionCountLast24Hours = smallTransactionCountLast24Hours,
            SmallTransactionTotalLast24Hours = decimal.Round(smallTransactionTotalLast24Hours, 2, MidpointRounding.AwayFromZero),
            DistinctReceiverCountLast24Hours = distinctReceiverCountLast24Hours,
            DistinctSenderCountToReceiverLast24Hours = distinctSenderCountToReceiverLast24Hours,
            RecentInboundAmountLast30Minutes = decimal.Round(recentInboundAmountLast30Minutes, 2, MidpointRounding.AwayFromZero),
            SenderAccountAgeDays = senderAccountAgeDays,
            SenderDaysSinceLastTransaction = senderDaysSinceLastTransaction,
            DetectionSettings = detectionSettings
        };
    }

    private async Task<SuspiciousDetectionSettingsDto?> BuildDetectionSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await _dbContext.FraudRuleSettings
            .AsNoTracking()
            .OrderBy(setting => setting.RuleCode)
            .Select(setting => new SuspiciousDetectionRuleSettingDto
            {
                RuleCode = setting.RuleCode,
                IsEnabled = setting.IsEnabled,
                Score = setting.Score,
                NumericThreshold = setting.NumericThreshold,
                AmountThresholdMnt = setting.AmountThresholdMnt,
                AmountThresholdUsd = setting.AmountThresholdUsd
            })
            .ToListAsync(cancellationToken);

        if (settings.Count == 0)
        {
            return null;
        }

        var suspiciousThreshold = await _dbContext.FraudDetectionSettings
            .AsNoTracking()
            .Select(setting => setting.SuspiciousThreshold)
            .FirstOrDefaultAsync(cancellationToken);

        return new SuspiciousDetectionSettingsDto
        {
            SuspiciousThreshold = suspiciousThreshold <= 0 ? 60 : suspiciousThreshold,
            Rules = settings
        };
    }

    private static decimal GetSmallAmountThreshold(string sourceCurrency)
    {
        return sourceCurrency switch
        {
            "MNT" => 10_000m,
            "USD" => 3m,
            _ => 3m
        };
    }

    private static string BuildSuspiciousReason(SuspiciousDetectionResultDto result)
    {
        if (result.TriggeredRules.Count == 0)
        {
            return result.Reason;
        }

        return $"{result.Reason} Илэрсэн rule: {string.Join(", ", result.TriggeredRules)}";
    }

    public async Task<ReceiverAccountPreviewDto?> GetReceiverAccountPreviewAsync(
        long currentUserId,
        string accountNumber,
        CancellationToken cancellationToken = default)
    {
        var normalizedAccountNumber = accountNumber.Trim();
        if (string.IsNullOrWhiteSpace(normalizedAccountNumber))
        {
            return null;
        }

        var receiver = await _dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.AccountNumber == normalizedAccountNumber && account.IsActive)
            .Select(account => new
            {
                account.AccountNumber,
                account.Currency,
                account.User.FirstName,
                account.User.LastName
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (receiver is null)
        {
            return null;
        }

        return new ReceiverAccountPreviewDto
        {
            AccountNumber = receiver.AccountNumber,
            Currency = receiver.Currency,
            OwnerDisplayName = BuildOwnerDisplayName(receiver.FirstName, receiver.LastName, maskLastName: true)
        };
    }

    public async Task<TransactionReceiptDto?> GetMyTransactionReceiptAsync(
        long currentUserId,
        long transactionId,
        CancellationToken cancellationToken = default)
    {
        var receipt = await _dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.Id == transactionId &&
                (transaction.FromAccount.UserId == currentUserId || transaction.ToAccount.UserId == currentUserId))
            .Select(transaction => new
            {
                transaction.Id,
                transaction.Amount,
                transaction.SourceCurrency,
                transaction.CreatedAt,
                SenderRemainingBalance = transaction.FromAccount.Balance,
                ReceiverFirstName = transaction.ToAccount.User.FirstName,
                ReceiverLastName = transaction.ToAccount.User.LastName,
                ReceiverAccountNumber = transaction.ToAccount.AccountNumber,
                transaction.Description
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (receipt is null)
        {
            return null;
        }

        return new TransactionReceiptDto
        {
            Id = receipt.Id,
            Amount = receipt.Amount,
            SourceCurrency = receipt.SourceCurrency,
            CreatedAt = receipt.CreatedAt,
            SenderRemainingBalance = receipt.SenderRemainingBalance,
            ReceiverOwnerName = BuildOwnerDisplayName(receipt.ReceiverFirstName, receipt.ReceiverLastName, maskLastName: true),
            ReceiverAccountNumber = receipt.ReceiverAccountNumber,
            Description = receipt.Description
        };
    }

    private IQueryable<UserTransactionDto> BuildMyTransactionsQuery(long currentUserId, long? accountId)
    {
        var query = _dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.FromAccount.UserId == currentUserId ||
                transaction.ToAccount.UserId == currentUserId);

        if (accountId is not null)
        {
            query = query.Where(transaction =>
                transaction.FromAccountId == accountId.Value ||
                transaction.ToAccountId == accountId.Value);
        }

        return query
            .OrderByDescending(transaction => transaction.CreatedAt)
            .ThenByDescending(transaction => transaction.Id)
            .Select(transaction => new UserTransactionDto
            {
                Id = transaction.Id,
                FromAccountNumber = transaction.FromAccount.AccountNumber,
                ToAccountNumber = transaction.ToAccount.AccountNumber,
                Amount = transaction.Amount,
                SourceCurrency = transaction.SourceCurrency,
                CreditedAmount = transaction.CreditedAmount,
                TargetCurrency = transaction.TargetCurrency,
                Description = transaction.Description,
                Status = transaction.Status,
                Direction = accountId == null
                    ? (transaction.FromAccount.UserId == currentUserId ? "SENT" : "RECEIVED")
                    : (transaction.FromAccountId == accountId.Value ? "SENT" : "RECEIVED"),
                CreatedAt = transaction.CreatedAt
            });
    }

    private async Task<(bool Success, string? ErrorMessage, long? ExchangeRateLogId, decimal? ExchangeRateValue, decimal CreditedAmount, decimal RoundingDifference, FxIncomeLog? FxIncome)>
        CalculateCreditedAmountAsync(string sourceCurrency, string targetCurrency, decimal amount, CancellationToken cancellationToken)
    {
        if (sourceCurrency == targetCurrency)
        {
            return (true, null, null, null, amount, 0m, null);
        }

        var quote = await _exchangeRateService.GetExchangeRateQuoteAsync(sourceCurrency, targetCurrency, cancellationToken);
        var exchangeRate = await _exchangeRateService.GetOrCreateLatestExchangeRateLogAsync(sourceCurrency, targetCurrency, cancellationToken);

        if (quote is null || exchangeRate is null)
        {
            return (false, $"{sourceCurrency}-{targetCurrency} валютын ханш олдсонгүй.", null, null, 0m, 0m, null);
        }

        var rawCreditedAmount = amount * exchangeRate.Rate;
        var creditedAmount = decimal.Truncate(rawCreditedAmount * 100m) / 100m;
        var roundingDifference = decimal.Round(rawCreditedAmount - creditedAmount, 4, MidpointRounding.AwayFromZero);
        var fxIncome = BuildFxIncomeLog(sourceCurrency, targetCurrency, amount, creditedAmount, quote);

        return (true, null, exchangeRate.Id, exchangeRate.Rate, creditedAmount, roundingDifference, fxIncome);
    }

    private async Task<decimal> GetTodayDebitTotalMntAsync(long accountId, string accountCurrency, CancellationToken cancellationToken)
    {
        var todayStart = MongoliaClock.Today.ToDateTime(TimeOnly.MinValue);
        var tomorrowStart = todayStart.AddDays(1);

        var todayDebitTotal = await _dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.FromAccountId == accountId &&
                transaction.Status == "SUCCESS" &&
                transaction.CreatedAt >= todayStart &&
                transaction.CreatedAt < tomorrowStart)
            .SumAsync(transaction => (decimal?)transaction.Amount, cancellationToken) ?? 0m;

        if (string.Equals(accountCurrency, "MNT", StringComparison.OrdinalIgnoreCase))
        {
            return todayDebitTotal;
        }

        var converted = await ConvertDebitAmountToMntAsync(accountCurrency, todayDebitTotal, cancellationToken);
        return converted.AmountMnt;
    }

    private async Task<(bool Success, string? ErrorMessage, decimal AmountMnt)> ConvertDebitAmountToMntAsync(
        string sourceCurrency,
        decimal amount,
        CancellationToken cancellationToken)
    {
        if (string.Equals(sourceCurrency, "MNT", StringComparison.OrdinalIgnoreCase))
        {
            return (true, null, amount);
        }

        var exchangeRate = await _exchangeRateService.GetOrCreateLatestExchangeRateLogAsync(sourceCurrency, "MNT", cancellationToken);
        if (exchangeRate is null)
        {
            return (false, $"{sourceCurrency}-MNT валютын ханш олдсонгүй.", 0m);
        }

        return (true, null, TruncateMoney(amount * exchangeRate.Rate));
    }

    private static FxIncomeLog? BuildFxIncomeLog(
        string sourceCurrency,
        string targetCurrency,
        decimal sourceAmount,
        decimal creditedAmount,
        BankWebApp.Web.DTOs.ExchangeRates.ExchangeRateQuoteDto quote)
    {
        if (quote.OfficialMntPerUsdRate is null || quote.CustomerMntPerUsdRate is null)
        {
            return null;
        }

        var officialRate = quote.OfficialMntPerUsdRate.Value;
        var customerRate = quote.CustomerMntPerUsdRate.Value;
        decimal spreadMargin;
        decimal incomeAmountMnt;
        string incomeType;

        if (sourceCurrency == "USD" && targetCurrency == "MNT")
        {
            spreadMargin = officialRate - customerRate;
            incomeAmountMnt = sourceAmount * spreadMargin;
            incomeType = "FX_BUY_SPREAD";
        }
        else if (sourceCurrency == "MNT" && targetCurrency == "USD")
        {
            spreadMargin = customerRate - officialRate;
            incomeAmountMnt = creditedAmount * spreadMargin;
            incomeType = "FX_SELL_SPREAD";
        }
        else
        {
            return null;
        }

        var normalizedIncome = TruncateMoney(Math.Max(0m, incomeAmountMnt));
        if (normalizedIncome <= 0)
        {
            return null;
        }

        return new FxIncomeLog
        {
            FromCurrency = sourceCurrency,
            ToCurrency = targetCurrency,
            SourceAmount = sourceAmount,
            CreditedAmount = creditedAmount,
            OfficialRateMntPerUsd = officialRate,
            CustomerRateMntPerUsd = customerRate,
            SpreadMarginMntPerUsd = Math.Max(0m, spreadMargin),
            IncomeAmountMnt = normalizedIncome,
            IncomeType = incomeType,
            Source = quote.Source,
            RateDate = quote.RateDate
        };
    }

    private static decimal TruncateMoney(decimal value)
    {
        return decimal.Truncate(value * 100m) / 100m;
    }

    private static (bool Success, string? ErrorMessage, long? TransactionId) Failed(string message)
    {
        return (false, message, null);
    }

    private static string BuildOwnerDisplayName(string? firstName, string? lastName, bool maskLastName)
    {
        var displayLastName = maskLastName ? MaskLastName(lastName) : lastName;
        var fullName = $"{firstName} {displayLastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? "-" : fullName;
    }

    private static string? MaskLastName(string? lastName)
    {
        if (string.IsNullOrWhiteSpace(lastName))
        {
            return null;
        }

        var trimmed = lastName.Trim();
        if (trimmed.Length <= 2)
        {
            return trimmed;
        }

        return $"{trimmed[0]}{new string('*', trimmed.Length - 2)}{trimmed[^1]}";
    }
}
