namespace BankWebApp.Web.DTOs.Admin;

public class AdminSuspiciousTransactionFilterDto
{
    public string? Search { get; set; }
    public string? AccountNumber { get; set; }
    public string? Username { get; set; }
    public string? ReviewStatus { get; set; }
    public string? Currency { get; set; }
    public decimal? MinRiskScore { get; set; }
    public decimal? MaxRiskScore { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}
