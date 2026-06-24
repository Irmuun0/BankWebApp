namespace BankWebApp.Web.DTOs.Admin;

public class AdminSuspiciousDetectionRuleSummaryDto
{
    public string RuleCode { get; set; } = string.Empty;
    public int HitCount { get; set; }
    public decimal AverageRiskScore { get; set; }
}
