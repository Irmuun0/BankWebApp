namespace BankWebApp.Web.DTOs.Admin;

public class AdminAiDetectionTransactionDto
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string FromAccountNumber { get; set; } = string.Empty;
    public string FromUserName { get; set; } = string.Empty;
    public string ToAccountNumber { get; set; } = string.Empty;
    public string ToUserName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string SourceCurrency { get; set; } = string.Empty;
    public decimal CreditedAmount { get; set; }
    public string TargetCurrency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSuspicious { get; set; }
    public decimal? RuleRiskScore { get; set; }
    public string? RuleReason { get; set; }
    public DateTime? RuleCheckedAt { get; set; }
    public bool? LatestAiIsSuspicious { get; set; }
    public decimal? LatestAiRiskScore { get; set; }
    public string? LatestAiExplanation { get; set; }
    public DateTime? LatestAiAnalyzedAt { get; set; }
}
