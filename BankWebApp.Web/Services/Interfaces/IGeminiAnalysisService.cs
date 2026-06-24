using BankWebApp.Web.DTOs.Ai;

namespace BankWebApp.Web.Services.Interfaces;

public interface IGeminiAnalysisService
{
    Task<(bool Success, GeminiTransactionAnalysisResultDto? Result, string? ErrorMessage)> AnalyzeTransactionAsync(
        GeminiSuspiciousAnalysisContextDto context,
        string? modelName = null,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Analysis, string? ErrorMessage)> AnalyzeSuspiciousTransactionAsync(
        GeminiSuspiciousAnalysisContextDto context,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Answer, string? ErrorMessage)> AskTransactionAnalysisQuestionAsync(
        GeminiSuspiciousAnalysisContextDto context,
        string existingAnalysis,
        string question,
        string? modelName = null,
        CancellationToken cancellationToken = default);
}
