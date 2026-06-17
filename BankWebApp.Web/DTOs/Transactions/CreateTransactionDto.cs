namespace BankWebApp.Web.DTOs.Transactions;

public class CreateTransactionDto
{
    public long FromAccountId { get; set; }
    public string ToAccountNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}
