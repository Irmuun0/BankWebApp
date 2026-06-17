using System;
using System.Collections.Generic;

namespace BankWebApp.Web.Data.Entities;

public partial class CurrencyRateSetting
{
    public long Id { get; set; }

    public string CurrencyCode { get; set; } = null!;

    public string BaseCurrency { get; set; } = null!;

    public decimal BaseRate { get; set; }

    public decimal AlgoBuyMarginPercent { get; set; }

    public decimal AlgoSellMarginPercent { get; set; }

    public decimal AlgoBuyRate { get; set; }

    public decimal AlgoSellRate { get; set; }

    public bool IsManualOverride { get; set; }

    public decimal? ManualBuyRate { get; set; }

    public decimal? ManualSellRate { get; set; }

    public DateTime? ManualExpiresAt { get; set; }

    public DateOnly RateDate { get; set; }

    public string Source { get; set; } = null!;

    public DateTime FetchedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual ICollection<CurrencyRateSettingAudit> CurrencyRateSettingAudits { get; set; } = new List<CurrencyRateSettingAudit>();

    public virtual ICollection<CurrencyRateOverrideSchedule> CurrencyRateOverrideSchedules { get; set; } = new List<CurrencyRateOverrideSchedule>();

    public virtual User? UpdatedByNavigation { get; set; }
}
