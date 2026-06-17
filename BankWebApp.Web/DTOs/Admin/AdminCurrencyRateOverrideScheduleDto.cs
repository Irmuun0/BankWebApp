namespace BankWebApp.Web.DTOs.Admin;

public class AdminCurrencyRateOverrideScheduleDto
{
    public long Id { get; set; }

    public long SettingId { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public string BaseCurrency { get; set; } = string.Empty;

    public decimal ManualBuyRate { get; set; }

    public decimal ManualSellRate { get; set; }

    public DateTime StartsAt { get; set; }

    public DateTime EndsAt { get; set; }

    public string Status { get; set; } = string.Empty;

    public string DisplayStatus { get; set; } = string.Empty;

    public string? CreatedByUsername { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? CancelledByUsername { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? Note { get; set; }
}
