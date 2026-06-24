namespace BankWebApp.Web.DTOs.Ai;

public class GeminiTransactionAnalysisResultDto
{
    public bool? IsSuspicious { get; set; }
    public decimal? RiskScore { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public string? RecommendedAction { get; set; }
    public string ModelName { get; set; } = string.Empty;
}
