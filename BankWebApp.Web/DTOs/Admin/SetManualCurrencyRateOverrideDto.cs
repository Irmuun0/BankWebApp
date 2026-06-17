namespace BankWebApp.Web.DTOs.Admin;

public class SetManualCurrencyRateOverrideDto
{
    public long SettingId { get; set; }

    public string AdjustmentMode { get; set; } = "PERCENT";

    public decimal BuyAdjustment { get; set; }

    public decimal SellAdjustment { get; set; }

    public DateTime StartsAt { get; set; }

    public DateTime EndsAt { get; set; }

    public string? Note { get; set; }
}
