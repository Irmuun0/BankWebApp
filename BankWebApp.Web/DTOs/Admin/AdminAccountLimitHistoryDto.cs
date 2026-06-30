namespace BankWebApp.Web.DTOs.Admin;

public class AdminAccountLimitHistoryDto
{
    public long Id { get; set; }
    public decimal? OldLimitAmount { get; set; }
    public decimal? NewLimitAmount { get; set; }
    public string? ChangedByUsername { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}
