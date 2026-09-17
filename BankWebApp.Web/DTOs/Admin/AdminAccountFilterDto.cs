namespace BankWebApp.Web.DTOs.Admin;

public class AdminAccountFilterDto
{
    public string? Search { get; set; }
    public string? Username { get; set; }
    public string? OwnerName { get; set; }
    public string? AccountNumber { get; set; }
    public string? Currency { get; set; }
    public string? AccountType { get; set; }
    public string? Status { get; set; }
    public decimal? MinBalance { get; set; }
    public decimal? MaxBalance { get; set; }
    public decimal? MinDailyLimitMnt { get; set; }
    public decimal? MaxDailyLimitMnt { get; set; }
    public DateOnly? CreatedFrom { get; set; }
    public DateOnly? CreatedTo { get; set; }
}
