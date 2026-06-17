namespace BankWebApp.Web.DTOs.Transactions;

public class TransactionReceiptDto
{
    public long Id { get; set; }
    public decimal Amount { get; set; }
    public string SourceCurrency { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public decimal SenderRemainingBalance { get; set; }
    public string ReceiverOwnerName { get; set; } = string.Empty;
    public string ReceiverAccountNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
}
