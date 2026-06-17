namespace BankWebApp.Web.DTOs.Ai;

public class SuspiciousDetectionResultDto
{
    public bool IsSuspicious { get; set; }
    public decimal RiskScore { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<string> TriggeredRules { get; set; } = [];
}
