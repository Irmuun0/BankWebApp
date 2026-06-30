using System;
using System.Collections.Generic;

namespace BankWebApp.Web.Data.Entities;

public partial class UserTransactionView
{
    public long Id { get; set; }

    public long FromAccountId { get; set; }

    public long ToAccountId { get; set; }

    public decimal Amount { get; set; }

    public string SourceCurrency { get; set; } = null!;

    public decimal CreditedAmount { get; set; }

    public string TargetCurrency { get; set; } = null!;

    public decimal? ExchangeRateValue { get; set; }

    public decimal RoundingDifference { get; set; }

    public string? Description { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
