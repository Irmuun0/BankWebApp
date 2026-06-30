namespace BankWebApp.Web.DTOs.Admin;

public class UpdateAccountTransactionLimitDto
{
    public long AccountId { get; set; }
    public decimal DailyLimitMnt { get; set; }
    public string? Reason { get; set; }
}
