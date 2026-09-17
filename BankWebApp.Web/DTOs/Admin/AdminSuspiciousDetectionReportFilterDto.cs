namespace BankWebApp.Web.DTOs.Admin;

public class AdminSuspiciousDetectionReportFilterDto
{
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Search { get; set; }
    public string? ReviewStatus { get; set; }
    public string? ServiceStatus { get; set; }
    public string? RuleCode { get; set; }
    public decimal? MinRiskScore { get; set; }
    public decimal? MaxRiskScore { get; set; }
    public bool SuspiciousOnly { get; set; }
}
