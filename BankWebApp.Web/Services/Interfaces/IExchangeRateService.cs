using BankWebApp.Web.DTOs.ExchangeRates;
using BankWebApp.Web.Data.Entities;

namespace BankWebApp.Web.Services.Interfaces;

public interface IExchangeRateService
{
    Task<IReadOnlyList<MongolBankExchangeRateDto>> GetMongolBankMntRatesAsync(CancellationToken cancellationToken = default);
    Task<ExchangeRateQuoteDto?> GetExchangeRateQuoteAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken = default);
    Task<ExchangeRateLog?> GetOrCreateLatestExchangeRateLogAsync(string fromCurrency, string toCurrency, CancellationToken cancellationToken = default);
}
