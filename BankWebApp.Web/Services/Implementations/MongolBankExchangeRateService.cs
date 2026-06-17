using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using BankWebApp.Web.Data;
using BankWebApp.Web.Data.Entities;
using BankWebApp.Web.DTOs.ExchangeRates;
using BankWebApp.Web.Helpers;
using BankWebApp.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BankWebApp.Web.Services.Implementations;

public class MongolBankExchangeRateService : IExchangeRateService
{
    private const string MongolBankRatesUrl = "https://www.mongolbank.mn/en/currency-rate-movement/data";
    private const string SourceName = "MNB_API";
    private const string AlgorithmSourceName = "ALGORITHM";
    private const string ManualOverrideSourceName = "MANUAL_OVERRIDE";
    private const decimal DefaultBuyMarginPercent = 0.0005m;
    private const decimal DefaultSellMarginPercent = 0.001m;
    private const decimal TroyOunceGrams = 31.1034768m;
    private static readonly SemaphoreSlim CacheLock = new(1, 1);
    private static IReadOnlyList<MongolBankExchangeRateDto>? CachedRates;
    private static DateTimeOffset? CachedAt;

    private readonly HttpClient _httpClient;
    private readonly IDbContextFactory<BankDbContext> _dbContextFactory;

    public MongolBankExchangeRateService(HttpClient httpClient, IDbContextFactory<BankDbContext> dbContextFactory)
    {
        _httpClient = httpClient;
        _dbContextFactory = dbContextFactory;
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<IReadOnlyList<MongolBankExchangeRateDto>> GetMongolBankMntRatesAsync(CancellationToken cancellationToken = default)
    {
        if (IsCacheFresh())
        {
            return CachedRates!;
        }

        await CacheLock.WaitAsync(cancellationToken);
        try
        {
            if (IsCacheFresh())
            {
                return CachedRates!;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, MongolBankRatesUrl);
            request.Headers.UserAgent.ParseAdd("BankWebApp/1.0");
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.Accept.ParseAdd("application/xml");
            request.Headers.Accept.ParseAdd("text/xml");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var rates = ParseRates(body);

            CachedRates = rates;
            CachedAt = DateTimeOffset.UtcNow;
            return CachedRates;
        }
        catch
        {
            return CachedRates ?? [];
        }
        finally
        {
            CacheLock.Release();
        }
    }

    public async Task<ExchangeRateQuoteDto?> GetExchangeRateQuoteAsync(
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken = default)
    {
        var normalizedFrom = NormalizeCurrency(fromCurrency);
        var normalizedTo = NormalizeCurrency(toCurrency);
        if (string.IsNullOrWhiteSpace(normalizedFrom) || string.IsNullOrWhiteSpace(normalizedTo))
        {
            return null;
        }

        if (normalizedFrom == normalizedTo)
        {
            return new ExchangeRateQuoteDto
            {
                FromCurrency = normalizedFrom,
                ToCurrency = normalizedTo,
                Rate = 1m,
                OfficialMntPerUsdRate = null,
                CustomerMntPerUsdRate = null,
                RateDate = MongoliaClock.Today,
                Source = SourceName,
                DisplayText = $"1 {normalizedFrom} = 1 {normalizedTo}"
            };
        }

        var mongolBankQuote = await BuildMntUsdQuoteFromMongolBankAsync(normalizedFrom, normalizedTo, cancellationToken);
        if (mongolBankQuote is not null)
        {
            return mongolBankQuote;
        }

        return await BuildStoredQuoteAsync(normalizedFrom, normalizedTo, cancellationToken);
    }

    public async Task<ExchangeRateLog?> GetOrCreateLatestExchangeRateLogAsync(
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken = default)
    {
        var quote = await GetExchangeRateQuoteAsync(fromCurrency, toCurrency, cancellationToken);
        if (quote is null || quote.FromCurrency == quote.ToCurrency)
        {
            return null;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingLog = await dbContext.ExchangeRateLogs
            .FirstOrDefaultAsync(rate =>
                    rate.FromCurrency == quote.FromCurrency &&
                    rate.ToCurrency == quote.ToCurrency &&
                    rate.RateDate == quote.RateDate &&
                    rate.Source == quote.Source,
                cancellationToken);

        if (existingLog is not null)
        {
            if (existingLog.Rate != quote.Rate)
            {
                existingLog.Rate = quote.Rate;
                existingLog.FetchedAt = MongoliaClock.Now;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return existingLog;
        }

        var newLog = new ExchangeRateLog
        {
            FromCurrency = quote.FromCurrency,
            ToCurrency = quote.ToCurrency,
            Rate = quote.Rate,
            RateDate = quote.RateDate,
            Source = quote.Source,
            FetchedAt = MongoliaClock.Now
        };

        dbContext.ExchangeRateLogs.Add(newLog);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return newLog;
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(newLog).State = EntityState.Detached;
            return await dbContext.ExchangeRateLogs
                .FirstOrDefaultAsync(rate =>
                        rate.FromCurrency == quote.FromCurrency &&
                        rate.ToCurrency == quote.ToCurrency &&
                        rate.RateDate == quote.RateDate &&
                        rate.Source == quote.Source,
                    cancellationToken);
        }
    }

    private static IReadOnlyList<MongolBankExchangeRateDto> ParseRates(string body)
    {
        var trimmed = body.TrimStart();
        return trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal)
            ? ParseJsonRates(body)
            : ParseXmlRates(body);
    }

    private static IReadOnlyList<MongolBankExchangeRateDto> ParseJsonRates(string body)
    {
        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var today = MongoliaClock.Today;
        JsonElement? fallbackRow = null;
        DateOnly? fallbackDate = null;

        foreach (var row in data.EnumerateArray())
        {
            var rowDate = NormalizePublishedRateDate(ReadDate(row));
            if (rowDate == today)
            {
                return ParseJsonRateRow(row, rowDate);
            }

            if (rowDate is not null && rowDate <= today && (fallbackDate is null || rowDate > fallbackDate))
            {
                fallbackRow = row;
                fallbackDate = rowDate;
            }
        }

        if (fallbackRow is null)
        {
            return [];
        }

        return ParseJsonRateRow(fallbackRow.Value, fallbackDate);
    }

    private static IReadOnlyList<MongolBankExchangeRateDto> ParseJsonRateRow(JsonElement selectedRow, DateOnly? selectedDate)
    {
        var rates = new List<MongolBankExchangeRateDto>();
        foreach (var property in selectedRow.EnumerateObject())
        {
            var code = property.Name.Trim().ToUpperInvariant();
            if (code == "RATE_DATE" || code.Length != 3)
            {
                continue;
            }

            var rate = ParseRate(property.Value);
            if (rate is not null)
            {
                rates.Add(new MongolBankExchangeRateDto
                {
                    CurrencyCode = code,
                    MntRate = NormalizeRate(code, rate.Value),
                    RateDate = selectedDate
                });
            }
        }

        return rates.OrderBy(rate => rate.CurrencyCode).ToList();
    }

    private static bool IsCacheFresh()
    {
        if (CachedRates is null || CachedAt is null)
        {
            return false;
        }

        var latestRateDate = CachedRates
            .Select(rate => rate.RateDate)
            .Where(rateDate => rateDate is not null)
            .Max();
        var cacheDuration = latestRateDate == MongoliaClock.Today
            ? TimeSpan.FromHours(6)
            : TimeSpan.FromMinutes(10);

        return DateTimeOffset.UtcNow - CachedAt < cacheDuration;
    }

    private static IReadOnlyList<MongolBankExchangeRateDto> ParseXmlRates(string body)
    {
        var document = XDocument.Parse(body, LoadOptions.None);
        var rates = new List<MongolBankExchangeRateDto>();

        foreach (var row in document.Descendants("Ccy"))
        {
            var code = row.Element("CcyNm_EN")?.Value.Trim().ToUpperInvariant();
            var rate = ParseRate(row.Element("Rate")?.Value);

            if (!string.IsNullOrWhiteSpace(code) && code.Length == 3 && rate is not null)
            {
                rates.Add(new MongolBankExchangeRateDto
                {
                    CurrencyCode = code,
                    MntRate = NormalizeRate(code, rate.Value),
                    RateDate = MongoliaClock.Today
                });
            }
        }

        return rates.OrderBy(rate => rate.CurrencyCode).ToList();
    }

    private static DateOnly? ReadDate(JsonElement row)
    {
        if (!row.TryGetProperty("RATE_DATE", out var value))
        {
            return null;
        }

        return DateOnly.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    private static DateOnly? NormalizePublishedRateDate(DateOnly? mongolBankRateDate)
    {
        if (mongolBankRateDate is null)
        {
            return null;
        }

        var effectiveDate = mongolBankRateDate.Value.AddDays(1);
        var today = MongoliaClock.Today;
        return effectiveDate > today ? today : effectiveDate;
    }

    private static decimal? ParseRate(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var number) && number != 0 => number,
            JsonValueKind.String => ParseRate(value.GetString()),
            _ => null
        };
    }

    private static decimal? ParseRate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = value.Trim().Replace(",", string.Empty).Replace(" ", string.Empty).Replace("\u00a0", string.Empty);
        if (cleaned is "-" or "0")
        {
            return null;
        }

        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate) && rate != 0
            ? rate
            : null;
    }

    private static decimal NormalizeRate(string currencyCode, decimal rate)
    {
        return currencyCode is "XAU" or "XAG"
            ? rate / TroyOunceGrams
            : rate;
    }

    private async Task<ExchangeRateQuoteDto?> BuildMntUsdQuoteFromMongolBankAsync(
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken)
    {
        if (!IsMntUsdPair(fromCurrency, toCurrency))
        {
            return null;
        }

        var rates = await GetMongolBankMntRatesAsync(cancellationToken);
        var usdRate = rates.FirstOrDefault(rate => rate.CurrencyCode == "USD");
        if (usdRate is null || usdRate.MntRate <= 0 || usdRate.RateDate is null)
        {
            return null;
        }

        var setting = await TryUpsertCurrencyRateSettingAsync(usdRate, cancellationToken);
        return setting is not null
            ? BuildQuoteFromSetting(fromCurrency, toCurrency, setting)
            : BuildQuoteFromBaseRate(fromCurrency, toCurrency, usdRate.MntRate, usdRate.RateDate.Value);
    }

    private async Task<ExchangeRateQuoteDto?> BuildStoredQuoteAsync(
        string fromCurrency,
        string toCurrency,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var storedRate = await dbContext.ExchangeRateLogs
            .AsNoTracking()
            .Where(rate => rate.FromCurrency == fromCurrency && rate.ToCurrency == toCurrency)
            .OrderByDescending(rate => rate.RateDate)
            .ThenByDescending(rate => rate.FetchedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (storedRate is null)
        {
            return null;
        }

        return new ExchangeRateQuoteDto
        {
            FromCurrency = storedRate.FromCurrency,
            ToCurrency = storedRate.ToCurrency,
            Rate = storedRate.Rate,
            OfficialMntPerUsdRate = null,
            CustomerMntPerUsdRate = IsMntUsdPair(storedRate.FromCurrency, storedRate.ToCurrency)
                ? (storedRate.FromCurrency == "USD"
                    ? storedRate.Rate
                    : decimal.Round(1m / storedRate.Rate, 8, MidpointRounding.AwayFromZero))
                : null,
            RateDate = storedRate.RateDate,
            Source = storedRate.Source,
            DisplayText = BuildStoredDisplayText(storedRate)
        };
    }

    private static string BuildStoredDisplayText(ExchangeRateLog storedRate)
    {
        if (IsMntUsdPair(storedRate.FromCurrency, storedRate.ToCurrency))
        {
            var mntPerUsd = storedRate.FromCurrency == "USD"
                ? storedRate.Rate
                : decimal.Round(1m / storedRate.Rate, 2, MidpointRounding.AwayFromZero);

            return $"1 USD = {mntPerUsd:N0} MNT";
        }

        return $"1 {storedRate.FromCurrency} = {storedRate.Rate:N8} {storedRate.ToCurrency}";
    }

    private static bool IsMntUsdPair(string fromCurrency, string toCurrency)
    {
        return (fromCurrency == "USD" && toCurrency == "MNT") ||
               (fromCurrency == "MNT" && toCurrency == "USD");
    }

    private static string NormalizeCurrency(string currency)
    {
        return currency.Trim().ToUpperInvariant();
    }

    private async Task<CurrencyRateSetting?> TryUpsertCurrencyRateSettingAsync(
        MongolBankExchangeRateDto usdRate,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var setting = await dbContext.CurrencyRateSettings
                .Include(rate => rate.CurrencyRateOverrideSchedules)
                .FirstOrDefaultAsync(rate => rate.CurrencyCode == "USD" && rate.BaseCurrency == "MNT", cancellationToken);

            if (setting is null)
            {
                setting = new CurrencyRateSetting
                {
                    CurrencyCode = "USD",
                    BaseCurrency = "MNT",
                    AlgoBuyMarginPercent = DefaultBuyMarginPercent,
                    AlgoSellMarginPercent = DefaultSellMarginPercent,
                    IsManualOverride = false
                };
                dbContext.CurrencyRateSettings.Add(setting);
            }

            var now = MongoliaClock.Now;
            var buyMargin = setting.AlgoBuyMarginPercent <= 0 ? DefaultBuyMarginPercent : setting.AlgoBuyMarginPercent;
            var sellMargin = setting.AlgoSellMarginPercent <= 0 ? DefaultSellMarginPercent : setting.AlgoSellMarginPercent;
            setting.BaseRate = usdRate.MntRate;
            setting.AlgoBuyMarginPercent = buyMargin;
            setting.AlgoSellMarginPercent = sellMargin;
            setting.AlgoBuyRate = CalculateBuyRate(usdRate.MntRate, buyMargin);
            setting.AlgoSellRate = CalculateSellRate(usdRate.MntRate, sellMargin);
            setting.RateDate = usdRate.RateDate ?? MongoliaClock.Today;
            setting.Source = SourceName;
            setting.FetchedAt = now;
            setting.UpdatedAt = now;

            if (setting.IsManualOverride &&
                setting.ManualExpiresAt is not null &&
                setting.ManualExpiresAt <= now)
            {
                setting.IsManualOverride = false;
                setting.ManualBuyRate = null;
                setting.ManualSellRate = null;
                setting.ManualExpiresAt = null;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return setting;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static ExchangeRateQuoteDto BuildQuoteFromSetting(
        string fromCurrency,
        string toCurrency,
        CurrencyRateSetting setting)
    {
        var now = MongoliaClock.Now;
        var activeSchedule = setting.CurrencyRateOverrideSchedules
            .Where(schedule =>
                schedule.Status != "CANCELLED" &&
                schedule.StartsAt <= now &&
                schedule.EndsAt > now)
            .OrderByDescending(schedule => schedule.StartsAt)
            .FirstOrDefault();
        var isManual = activeSchedule is not null ||
                       (setting.IsManualOverride &&
                        setting.ManualBuyRate is not null &&
                        setting.ManualSellRate is not null &&
                        (setting.ManualExpiresAt is null || setting.ManualExpiresAt > now));

        var buyRate = activeSchedule?.ManualBuyRate ?? (isManual ? setting.ManualBuyRate!.Value : setting.AlgoBuyRate);
        var sellRate = activeSchedule?.ManualSellRate ?? (isManual ? setting.ManualSellRate!.Value : setting.AlgoSellRate);
        var source = isManual ? ManualOverrideSourceName : AlgorithmSourceName;
        var displayRate = fromCurrency == "USD" ? buyRate : sellRate;
        var displayLabel = fromCurrency == "USD" ? "авах ханш" : "зарах ханш";
        var conversionRate = fromCurrency == "USD"
            ? buyRate
            : decimal.Round(1m / sellRate, 8, MidpointRounding.AwayFromZero);

        return new ExchangeRateQuoteDto
        {
            FromCurrency = fromCurrency,
            ToCurrency = toCurrency,
            Rate = conversionRate,
            OfficialMntPerUsdRate = setting.BaseRate,
            CustomerMntPerUsdRate = displayRate,
            RateDate = setting.RateDate,
            Source = source,
            DisplayText = $"1 USD = {displayRate:N0} MNT ({displayLabel})"
        };
    }

    private static ExchangeRateQuoteDto BuildQuoteFromBaseRate(
        string fromCurrency,
        string toCurrency,
        decimal baseRate,
        DateOnly rateDate)
    {
        var buyRate = CalculateBuyRate(baseRate, DefaultBuyMarginPercent);
        var sellRate = CalculateSellRate(baseRate, DefaultSellMarginPercent);
        var displayRate = fromCurrency == "USD" ? buyRate : sellRate;
        var displayLabel = fromCurrency == "USD" ? "авах ханш" : "зарах ханш";
        var conversionRate = fromCurrency == "USD"
            ? buyRate
            : decimal.Round(1m / sellRate, 8, MidpointRounding.AwayFromZero);

        return new ExchangeRateQuoteDto
        {
            FromCurrency = fromCurrency,
            ToCurrency = toCurrency,
            Rate = conversionRate,
            OfficialMntPerUsdRate = baseRate,
            CustomerMntPerUsdRate = displayRate,
            RateDate = rateDate,
            Source = AlgorithmSourceName,
            DisplayText = $"1 USD = {displayRate:N0} MNT ({displayLabel})"
        };
    }

    private static decimal CalculateBuyRate(decimal baseRate, decimal marginPercent)
    {
        return decimal.Floor(baseRate * (1m - marginPercent));
    }

    private static decimal CalculateSellRate(decimal baseRate, decimal marginPercent)
    {
        return decimal.Ceiling(baseRate * (1m + marginPercent));
    }
}
