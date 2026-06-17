namespace BankWebApp.Web.DTOs.Admin;

public class AdminFxIncomeDto
{
    public long Id { get; set; }

    public long TransactionId { get; set; }

    public string FromCurrency { get; set; } = string.Empty;

    public string ToCurrency { get; set; } = string.Empty;

    public decimal SourceAmount { get; set; }

    public decimal CreditedAmount { get; set; }

    public decimal OfficialRateMntPerUsd { get; set; }

    public decimal CustomerRateMntPerUsd { get; set; }

    public decimal SpreadMarginMntPerUsd { get; set; }

    public decimal IncomeAmountMnt { get; set; }

    public string IncomeType { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public DateOnly RateDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public string FromAccountNumber { get; set; } = string.Empty;

    public string ToAccountNumber { get; set; } = string.Empty;

    public string? Description { get; set; }
}
