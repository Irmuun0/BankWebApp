using System;

namespace BankWebApp.Web.Data.Entities;

public partial class FxIncomeLog
{
    public long Id { get; set; }

    public long TransactionId { get; set; }

    public string FromCurrency { get; set; } = null!;

    public string ToCurrency { get; set; } = null!;

    public decimal SourceAmount { get; set; }

    public decimal CreditedAmount { get; set; }

    public decimal OfficialRateMntPerUsd { get; set; }

    public decimal CustomerRateMntPerUsd { get; set; }

    public decimal SpreadMarginMntPerUsd { get; set; }

    public decimal IncomeAmountMnt { get; set; }

    public string IncomeType { get; set; } = null!;

    public string Source { get; set; } = null!;

    public DateOnly RateDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Transaction Transaction { get; set; } = null!;
}
