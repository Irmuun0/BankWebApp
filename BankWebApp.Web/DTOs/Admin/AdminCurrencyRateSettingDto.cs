namespace BankWebApp.Web.DTOs.Admin;

public class AdminCurrencyRateSettingDto
{
    public long Id { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public string BaseCurrency { get; set; } = string.Empty;

    public decimal BaseRate { get; set; }

    public decimal AlgoBuyMarginPercent { get; set; }

    public decimal AlgoSellMarginPercent { get; set; }

    public decimal AlgoBuyRate { get; set; }

    public decimal AlgoSellRate { get; set; }

    public bool IsManualOverride { get; set; }

    public bool IsManualOverrideActive { get; set; }

    public decimal? ManualBuyRate { get; set; }

    public decimal? ManualSellRate { get; set; }

    public DateTime? ManualExpiresAt { get; set; }

    public decimal ActiveBuyRate { get; set; }

    public decimal ActiveSellRate { get; set; }

    public DateOnly RateDate { get; set; }

    public string Source { get; set; } = string.Empty;

    public DateTime FetchedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? UpdatedByUsername { get; set; }

    public List<AdminCurrencyRateOverrideScheduleDto> OverrideSchedules { get; set; } = [];
}
