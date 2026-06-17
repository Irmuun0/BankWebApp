namespace BankWebApp.Web.DTOs.Admin;

public class AdminDashboardSummaryDto
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int InactiveUsers { get; set; }
    public int TotalAccounts { get; set; }
    public int ActiveAccounts { get; set; }
    public int TotalTransactions { get; set; }
    public int SuspiciousTransactions { get; set; }
    public int PendingReviews { get; set; }
    public int TodayTransactions { get; set; }
    public int TodaySuspiciousTransactions { get; set; }
    public decimal TodayFxIncomeMnt { get; set; }
}
