namespace BankWebApp.Web.DTOs.Accounts;

public class AccountSummaryDto
{
    public int TotalAccounts { get; set; }
    public decimal TotalMntBalance { get; set; }
    public decimal TotalUsdBalance { get; set; }
    public int ActiveAccountCount { get; set; }
    public int InactiveAccountCount { get; set; }
}
