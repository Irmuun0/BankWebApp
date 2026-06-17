namespace BankWebApp.Web.DTOs.ExchangeRates;

public class MongolBankExchangeRateDto
{
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal MntRate { get; set; }
    public DateOnly? RateDate { get; set; }
}
