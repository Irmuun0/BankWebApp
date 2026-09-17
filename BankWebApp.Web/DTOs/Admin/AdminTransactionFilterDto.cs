namespace BankWebApp.Web.DTOs.Admin;

public class AdminTransactionFilterDto
{
    public string? Search { get; set; }
    public string? AccountNumber { get; set; }
    public string? Username { get; set; }
    public string? Currency { get; set; }
    public string? Status { get; set; }
    public string? SuspiciousStatus { get; set; }
    public string? DetectionStatus { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}
