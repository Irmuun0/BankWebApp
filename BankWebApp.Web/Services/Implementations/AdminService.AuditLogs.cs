using System.Text.Json;
using BankWebApp.Web.Data.Entities;
using BankWebApp.Web.DTOs.Admin;
using BankWebApp.Web.Helpers;
using Microsoft.EntityFrameworkCore;

namespace BankWebApp.Web.Services.Implementations;

// AdminService-ийн AuditLogs үүрэгтэй хэсэг; тусдаа instance үүсгэхгүй.
public partial class AdminService
{
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
}
