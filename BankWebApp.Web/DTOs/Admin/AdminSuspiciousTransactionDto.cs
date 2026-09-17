namespace BankWebApp.Web.DTOs.Admin;

public class AdminSuspiciousTransactionDto
{
    public bool IsDraft { get; set; }
    public long TransactionId { get; set; }
    public long FromAccountId { get; set; }
    public string FromAccountNumber { get; set; } = string.Empty;
    public long FromUserId { get; set; }
    public string FromUserName { get; set; } = string.Empty;
    public long ToAccountId { get; set; }
    public string ToAccountNumber { get; set; } = string.Empty;
    public long ToUserId { get; set; }
    public string ToUserName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string SourceCurrency { get; set; } = string.Empty;
    public decimal CreditedAmount { get; set; }
    public string TargetCurrency { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public decimal RiskScore { get; set; }
    public string SuspiciousReason { get; set; } = string.Empty;
    public string? AiExplanation { get; set; }
    public DateTime ReviewCreatedAt { get; set; }
    public DateTime? DetectionLoggedAt { get; set; }
    public string? DetectionStatus { get; set; }
    public string? DetectionSource { get; set; }
    public decimal? DetectionRiskScore { get; set; }
    public string? DetectionReason { get; set; }
    public string? DetectionTriggeredRules { get; set; }
    public IReadOnlyList<string> DetectionRules { get; set; } = Array.Empty<string>();
    public string ReviewStatus { get; set; } = string.Empty;
    public string ReviewStatusLabel { get; set; } = string.Empty;
    public string? ReviewNote { get; set; }
    public long? ReviewedBy { get; set; }
    public string? ReviewedByUsername { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
