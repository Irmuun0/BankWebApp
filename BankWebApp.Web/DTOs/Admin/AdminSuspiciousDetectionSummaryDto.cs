namespace BankWebApp.Web.DTOs.Admin;

public class AdminSuspiciousDetectionSummaryDto
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int TotalChecks { get; set; }
    public int SuspiciousCount { get; set; }
    public int NormalCount { get; set; }
    public int UnavailableCount { get; set; }
    public int PendingReviewCount { get; set; }
    public int ReviewedCount { get; set; }
    public decimal AverageRiskScore { get; set; }
    public decimal MaxRiskScore { get; set; }
}
