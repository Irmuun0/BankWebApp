namespace BankWebApp.Web.DTOs.Admin;

public class AdminUserAccountSummaryDto
{
    public long Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public bool IsActive { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime OpenedAt { get; set; }
}
