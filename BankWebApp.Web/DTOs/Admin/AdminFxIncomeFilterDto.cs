namespace BankWebApp.Web.DTOs.Admin;

public class AdminFxIncomeFilterDto
{
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Search { get; set; }
    public string? AccountNumber { get; set; }
    public string? FromCurrency { get; set; }
    public string? ToCurrency { get; set; }
    public string? IncomeType { get; set; }
    public decimal? MinIncomeMnt { get; set; }
    public decimal? MaxIncomeMnt { get; set; }
}
