namespace BankWebApp.Web.DTOs.Admin;

public class AdminTransactionDto
{
    public long Id { get; set; }
    public string FromAccountNumber { get; set; } = string.Empty;
    public string ToAccountNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string SourceCurrency { get; set; } = string.Empty;
    public decimal CreditedAmount { get; set; }
    public string TargetCurrency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSuspicious { get; set; }
    public string? DetectionStatus { get; set; }
    public decimal? DetectionRiskScore { get; set; }
    public string? DetectionReason { get; set; }
    public string? DetectionTriggeredRules { get; set; }
    public DateTime? DetectionLoggedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
