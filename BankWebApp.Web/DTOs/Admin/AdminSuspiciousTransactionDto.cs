namespace BankWebApp.Web.DTOs.Admin;

public class AdminSuspiciousTransactionDto
{
    public long TransactionId { get; set; }
    public string FromAccountNumber { get; set; } = string.Empty;
    public string ToAccountNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string SourceCurrency { get; set; } = string.Empty;
    public decimal CreditedAmount { get; set; }
    public string TargetCurrency { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public decimal RiskScore { get; set; }
    public string SuspiciousReason { get; set; } = string.Empty;
    public string? AiExplanation { get; set; }
    public string ReviewStatus { get; set; } = string.Empty;
    public string ReviewStatusLabel { get; set; } = string.Empty;
    public string? ReviewNote { get; set; }
    public long? ReviewedBy { get; set; }
    public string? ReviewedByUsername { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
