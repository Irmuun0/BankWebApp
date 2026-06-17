namespace BankWebApp.Web.DTOs.Admin;

public class AdminFxIncomeSummaryDto
{
    public decimal TotalIncomeMnt { get; set; }

    public decimal BuyIncomeMnt { get; set; }

    public decimal SellIncomeMnt { get; set; }

    public decimal AverageSpreadMarginMntPerUsd { get; set; }

    public int TotalItems { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }
}
