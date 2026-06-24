namespace BankWebApp.Web.DTOs.Admin;

public class AdminSuspiciousDetectionLogDto
{
    public long Id { get; set; }
    public long TransactionId { get; set; }
    public DateTime DetectionAt { get; set; }
    public DateTime TransactionAt { get; set; }
    public string ServiceStatus { get; set; } = string.Empty;
    public bool? IsSuspicious { get; set; }
    public decimal? RiskScore { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string TriggeredRulesText { get; set; } = string.Empty;
    public List<string> TriggeredRules { get; set; } = [];
    public string ReviewStatus { get; set; } = string.Empty;
    public string ReviewStatusLabel { get; set; } = string.Empty;
    public string FromAccountNumber { get; set; } = string.Empty;
    public string ToAccountNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string SourceCurrency { get; set; } = string.Empty;
    public decimal CreditedAmount { get; set; }
    public string TargetCurrency { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}
