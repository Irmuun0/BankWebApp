namespace BankWebApp.Web.DTOs.Accounts;

public class AccountDto
{
    public long Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public decimal DailyTransactionLimitMnt { get; set; }
    public bool IsActive { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastTransactionAt { get; set; }
    public string? OwnerName { get; set; }
}
