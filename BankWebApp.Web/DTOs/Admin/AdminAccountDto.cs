namespace BankWebApp.Web.DTOs.Admin;

public class AdminAccountDto
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public decimal DailyTransactionLimitMnt { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
