using System;

namespace BankWebApp.Web.Data.Entities;

public partial class AccountTransactionLimitHistory
{
    public long Id { get; set; }

    public long AccountId { get; set; }

    public decimal? OldLimitAmount { get; set; }

    public decimal? NewLimitAmount { get; set; }

    public long? ChangedByUserId { get; set; }

    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Account Account { get; set; } = null!;

    public virtual User? ChangedByUser { get; set; }
}
