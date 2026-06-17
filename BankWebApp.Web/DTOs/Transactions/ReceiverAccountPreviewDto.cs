namespace BankWebApp.Web.DTOs.Transactions;

public class ReceiverAccountPreviewDto
{
    public string AccountNumber { get; set; } = string.Empty;
    public string OwnerDisplayName { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
}
