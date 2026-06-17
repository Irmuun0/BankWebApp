namespace BankWebApp.Web.DTOs.ExchangeRates;

public class ExchangeRateQuoteDto
{
    public string FromCurrency { get; set; } = string.Empty;
    public string ToCurrency { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public decimal? OfficialMntPerUsdRate { get; set; }
    public decimal? CustomerMntPerUsdRate { get; set; }
    public DateOnly RateDate { get; set; }
    public string Source { get; set; } = string.Empty;
    public string DisplayText { get; set; } = string.Empty;
}
