namespace BankWebApp.Web.DTOs.Transactions;

public class UserTransactionDto
{
    public long Id { get; set; }
    public string FromAccountNumber { get; set; } = string.Empty;
    public string ToAccountNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string SourceCurrency { get; set; } = string.Empty;
    public decimal CreditedAmount { get; set; }
    public string TargetCurrency { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
