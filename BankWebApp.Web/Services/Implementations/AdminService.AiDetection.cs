using System.Text.Json;
using BankWebApp.Web.Data.Entities;
using BankWebApp.Web.DTOs.Admin;
using BankWebApp.Web.DTOs.Ai;
using BankWebApp.Web.Helpers;
using Microsoft.EntityFrameworkCore;

namespace BankWebApp.Web.Services.Implementations;

// AdminService-ийн AiDetection үүрэгтэй хэсэг; тусдаа instance үүсгэхгүй.
public partial class AdminService
{
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
}
