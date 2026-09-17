using System.Text.Json;
using BankWebApp.Web.Data.Entities;
using BankWebApp.Web.DTOs.Admin;
using BankWebApp.Web.Helpers;
using Microsoft.EntityFrameworkCore;

namespace BankWebApp.Web.Services.Implementations;

// AdminService-ийн FraudReview үүрэгтэй хэсэг; тусдаа instance үүсгэхгүй.
public partial class AdminService
{
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
}
