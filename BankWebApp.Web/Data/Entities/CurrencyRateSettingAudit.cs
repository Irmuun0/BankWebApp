using System;

namespace BankWebApp.Web.Data.Entities;

public partial class CurrencyRateSettingAudit
{
    public long Id { get; set; }

    public long CurrencyRateSettingId { get; set; }

    public string Action { get; set; } = null!;

    public decimal? OldBuyRate { get; set; }

    public decimal? OldSellRate { get; set; }

    public decimal? NewBuyRate { get; set; }

    public decimal? NewSellRate { get; set; }

    public bool? OldIsManualOverride { get; set; }

    public bool? NewIsManualOverride { get; set; }

    public DateTime? OldManualExpiresAt { get; set; }

    public DateTime? NewManualExpiresAt { get; set; }

    public long? ChangedBy { get; set; }

    public DateTime ChangedAt { get; set; }

    public string? Note { get; set; }

    public virtual CurrencyRateSetting CurrencyRateSetting { get; set; } = null!;

    public virtual User? ChangedByNavigation { get; set; }
}
