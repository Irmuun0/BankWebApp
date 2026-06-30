namespace BankWebApp.Web.DTOs.Admin;

public class AdminAccountLimitDetailsDto
{
    public long AccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string? OwnerName { get; set; }
    public decimal CurrentDailyLimitMnt { get; set; }
    public List<AdminAccountLimitHistoryDto> Histories { get; set; } = [];
}
