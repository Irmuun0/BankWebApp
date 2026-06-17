namespace BankWebApp.Web.DTOs.Admin;

public class UpdateCurrencyRateAlgorithmDto
{
    public long SettingId { get; set; }

    public decimal BuyMarginPercent { get; set; }

    public decimal SellMarginPercent { get; set; }
}
