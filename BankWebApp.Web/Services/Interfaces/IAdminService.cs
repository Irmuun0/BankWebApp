using BankWebApp.Web.DTOs.Admin;

namespace BankWebApp.Web.Services.Interfaces;

public interface IAdminService
{
    Task<AdminDashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken cancellationToken = default);
    Task<AdminPagedResultDto<AdminUserDto>> GetUsersAsync(string? search = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<AdminPagedResultDto<AdminAccountDto>> GetAccountsAsync(string? search = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<AdminPagedResultDto<AdminTransactionDto>> GetTransactionsAsync(string? search = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<AdminPagedResultDto<AdminAuditLogDto>> GetAuditLogsAsync(string? source = null, string? search = null, DateOnly? startDate = null, DateOnly? endDate = null, int page = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<AdminAiDetectionPageDto> GetAiDetectionPageAsync(string? search = null, string? username = null, string? currency = null, decimal? minRiskScore = null, DateOnly? startDate = null, DateOnly? endDate = null, long? chatTransactionId = null, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<(bool Success, string? ErrorMessage, int AnalyzedCount)> AnalyzeTransactionsWithAiAsync(long adminUserId, IReadOnlyList<long> transactionIds, string? modelName = null, CancellationToken cancellationToken = default);
    Task<(bool Success, string? ErrorMessage)> AskAiDetectionQuestionAsync(long adminUserId, long transactionId, string question, string? modelName = null, CancellationToken cancellationToken = default);
    Task<AdminFxIncomeReportDto> GetFxIncomeReportAsync(DateOnly? startDate = null, DateOnly? endDate = null, string? search = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<AdminSuspiciousDetectionReportDto> GetSuspiciousDetectionReportAsync(DateOnly? startDate = null, DateOnly? endDate = null, string? search = null, string? reviewStatus = null, bool suspiciousOnly = false, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<List<AdminCurrencyRateSettingDto>> GetCurrencyRateSettingsAsync(CancellationToken cancellationToken = default);
    Task<(bool Success, string? ErrorMessage)> UpdateCurrencyRateAlgorithmAsync(long adminUserId, UpdateCurrencyRateAlgorithmDto dto, CancellationToken cancellationToken = default);
    Task<(bool Success, string? ErrorMessage)> SetManualCurrencyRateOverrideAsync(long adminUserId, SetManualCurrencyRateOverrideDto dto, CancellationToken cancellationToken = default);
    Task<(bool Success, string? ErrorMessage)> CancelCurrencyRateOverrideScheduleAsync(long adminUserId, long scheduleId, CancellationToken cancellationToken = default);
    Task<AdminFraudRuleSettingsPageDto> GetFraudRuleSettingsAsync(CancellationToken cancellationToken = default);
    Task<(bool Success, string? ErrorMessage)> UpdateFraudRuleSettingAsync(long adminUserId, UpdateFraudRuleSettingDto dto, CancellationToken cancellationToken = default);
    Task<(bool Success, string? ErrorMessage)> UpdateFraudDetectionSettingsAsync(long adminUserId, UpdateFraudDetectionSettingsDto dto, CancellationToken cancellationToken = default);
    Task<AdminPagedResultDto<AdminSuspiciousTransactionDto>> GetSuspiciousTransactionsAsync(string? search = null, string? reviewStatus = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<AdminSuspiciousTransactionDto?> GetSuspiciousTransactionDetailAsync(long transactionId, CancellationToken cancellationToken = default);
    Task<(bool Success, string? ErrorMessage)> UpdateSuspiciousReviewAsync(long adminUserId, UpdateSuspiciousReviewDto dto, CancellationToken cancellationToken = default);
    Task<(bool Success, string? ErrorMessage)> SetUserActiveStatusAsync(long adminUserId, long userId, bool isActive, CancellationToken cancellationToken = default);
    Task<(bool Success, string? ErrorMessage)> SetAccountActiveStatusAsync(long adminUserId, long accountId, bool isActive, CancellationToken cancellationToken = default);
}
