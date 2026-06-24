namespace BankWebApp.Web.DTOs.Ai;

public class GeminiSuspiciousAnalysisContextDto
{
    public long TransactionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal Amount { get; set; }
    public string SourceCurrency { get; set; } = string.Empty;
    public decimal CreditedAmount { get; set; }
    public string TargetCurrency { get; set; } = string.Empty;
    public decimal RiskScore { get; set; }
    public string SuspiciousReason { get; set; } = string.Empty;
    public string ReviewStatus { get; set; } = string.Empty;
    public string FromAccountMasked { get; set; } = string.Empty;
    public string ToAccountMasked { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsCrossCurrency { get; set; }
    public decimal? ExchangeRateValue { get; set; }
    public DateTime? DetectionCheckedAt { get; set; }
}
