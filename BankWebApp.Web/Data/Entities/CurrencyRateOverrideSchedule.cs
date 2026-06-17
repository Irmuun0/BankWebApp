using System;

namespace BankWebApp.Web.Data.Entities;

public partial class CurrencyRateOverrideSchedule
{
    public long Id { get; set; }

    public long CurrencyRateSettingId { get; set; }

    public decimal ManualBuyRate { get; set; }

    public decimal ManualSellRate { get; set; }

    public DateTime StartsAt { get; set; }

    public DateTime EndsAt { get; set; }

    public string Status { get; set; } = null!;

    public long? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CancelledBy { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? Note { get; set; }

    public virtual User? CancelledByNavigation { get; set; }

    public virtual User? CreatedByNavigation { get; set; }

    public virtual CurrencyRateSetting CurrencyRateSetting { get; set; } = null!;
}
