using System;
using System.Collections.Generic;

namespace BankWebApp.Web.Data.Entities;

public partial class ExchangeRateLog
{
    public long Id { get; set; }

    public string FromCurrency { get; set; } = null!;

    public string ToCurrency { get; set; } = null!;

    public decimal Rate { get; set; }

    public DateOnly RateDate { get; set; }

    public string Source { get; set; } = null!;

    public DateTime FetchedAt { get; set; }

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
