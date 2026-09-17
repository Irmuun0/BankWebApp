using System.Diagnostics;
using System.Net.Http.Json;
using BankWebApp.Web.Data;
using BankWebApp.Web.DTOs.Health;
using BankWebApp.Web.Helpers;
using BankWebApp.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BankWebApp.Web.Services.Implementations;

public class OperationalHealthService : IOperationalHealthService
{
    private readonly IDbContextFactory<BankDbContext> _dbContextFactory;
    private readonly IExchangeRateService _exchangeRateService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public OperationalHealthService(
        IDbContextFactory<BankDbContext> dbContextFactory,
        IExchangeRateService exchangeRateService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _dbContextFactory = dbContextFactory;
        _exchangeRateService = exchangeRateService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _environment = environment;
    }

    public async Task<OperationalHealthReportDto> CheckAsync(CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.StartNew();
        var checks = new[]
        {
            CheckWebAppAsync(cancellationToken),
            CheckDatabaseAsync(cancellationToken),
            CheckFastApiAsync(cancellationToken),
            CheckGeminiConfigAsync(cancellationToken),
            CheckMongolBankAsync(cancellationToken)
        };

        var components = await Task.WhenAll(checks);
        started.Stop();

        return new OperationalHealthReportDto
        {
            GeneratedAt = MongoliaClock.Now,
            DurationMs = started.ElapsedMilliseconds,
            Environment = _environment.EnvironmentName,
            Status = BuildOverallStatus(components),
            Components = components
                .OrderByDescending(component => component.IsCritical)
                .ThenBy(component => component.Key)
                .ToList()
        };
    }

    private Task<OperationalHealthComponentDto> CheckWebAppAsync(CancellationToken cancellationToken)
    {
        return RunCheckAsync(
            key: "web_app",
            name: "Phoebe Bank Web App",
            isCritical: true,
            endpoint: null,
            timeoutSeconds: 2,
            check: _ =>
            {
                var uptime = DateTimeOffset.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
                return Task.FromResult<HealthCheckResult>(new HealthCheckSuccess(
                    "Web application is running.",
                    $"Environment: {_environment.EnvironmentName}. Uptime: {FormatDuration(uptime)}."));
            },
            cancellationToken);
    }

    private Task<OperationalHealthComponentDto> CheckDatabaseAsync(CancellationToken cancellationToken)
    {
        return RunCheckAsync(
            key: "database",
            name: "MSSQL database",
            isCritical: true,
            endpoint: _configuration.GetConnectionString("DefaultConnection"),
            timeoutSeconds: 5,
            check: async ct =>
            {
                await using var dbContext = await _dbContextFactory.CreateDbContextAsync(ct);
                var canConnect = await dbContext.Database.CanConnectAsync(ct);
                if (!canConnect)
                {
                    return new HealthCheckFailure("Database connection failed.", null);
                }

                var userCount = await dbContext.Users.AsNoTracking().CountAsync(ct);
                return new HealthCheckSuccess("Database connection is ready.", $"Users table count: {userCount}.");
            },
            cancellationToken);
    }

    private Task<OperationalHealthComponentDto> CheckFastApiAsync(CancellationToken cancellationToken)
    {
        var baseUrl = BuildAiServiceBaseUrl();
        return RunCheckAsync(
            key: "fastapi_ai",
            name: "FastAPI AI service",
            isCritical: false,
            endpoint: $"{baseUrl}health",
            timeoutSeconds: 12,
            check: async ct =>
            {
                var client = _httpClientFactory.CreateClient();
                using var response = await client.GetAsync($"{baseUrl}health", ct);
                if (!response.IsSuccessStatusCode)
                {
                    return new HealthCheckFailure(
                        "FastAPI service responded with an error.",
                        $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                }

                var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(cancellationToken: ct);
                var serviceName = payload is not null && payload.TryGetValue("service", out var service)
                    ? service?.ToString()
                    : "bank-ai-service";
                return new HealthCheckSuccess("FastAPI service is ready.", $"Service: {serviceName}.");
            },
            cancellationToken);
    }

    private Task<OperationalHealthComponentDto> CheckGeminiConfigAsync(CancellationToken cancellationToken)
    {
        var baseUrl = BuildAiServiceBaseUrl();
        return RunCheckAsync(
            key: "gemini_api",
            name: "Gemini API readiness",
            isCritical: false,
            endpoint: $"{baseUrl}health/gemini",
            timeoutSeconds: 10,
            check: async ct =>
            {
                var client = _httpClientFactory.CreateClient();
                using var response = await client.GetAsync($"{baseUrl}health/gemini", ct);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return new HealthCheckWarning(
                        "Gemini config endpoint is not available.",
                        "Update ai_service to expose /health/gemini, or verify GEMINI_API_KEY manually.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    return new HealthCheckFailure(
                        "Gemini readiness check failed.",
                        $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
                }

                var payload = await response.Content.ReadFromJsonAsync<GeminiHealthResponse>(cancellationToken: ct);
                if (payload is
                    {
                        KeyConfigured: true,
                        ApiReachable: true,
                        ModelAvailable: true
                    })
                {
                    return new HealthCheckSuccess(
                        "Gemini endpoint and configured model are ready.",
                        $"Model: {payload.ModelName ?? "-"}. {payload.Message}");
                }

                if (payload?.KeyConfigured == true)
                {
                    return new HealthCheckWarning(
                        "Gemini is configured but not fully ready.",
                        payload.Message ?? "The configured model could not be verified.");
                }

                return new HealthCheckWarning(
                    "Gemini API key is not configured.",
                    "Set GEMINI_API_KEY in the FastAPI ai_service environment.");
            },
            cancellationToken);
    }

    private Task<OperationalHealthComponentDto> CheckMongolBankAsync(CancellationToken cancellationToken)
    {
        return RunCheckAsync(
            key: "mongolbank_api",
            name: "MongolBank exchange-rate API",
            isCritical: false,
            endpoint: "https://www.mongolbank.mn",
            timeoutSeconds: 8,
            check: async ct =>
            {
                var rates = await _exchangeRateService.GetMongolBankMntRatesAsync(ct);
                if (rates.Count == 0)
                {
                    return new HealthCheckWarning(
                        "No exchange-rate rows were returned.",
                        "The app may still use cached/stored exchange rates if available.");
                }

                var latestDate = rates
                    .Where(rate => rate.RateDate is not null)
                    .Select(rate => rate.RateDate!.Value)
                    .DefaultIfEmpty()
                    .Max();

                var detail = latestDate == default
                    ? $"Rates loaded: {rates.Count}."
                    : $"Rates loaded: {rates.Count}. Latest date: {latestDate:yyyy-MM-dd}.";
                return new HealthCheckSuccess("MongolBank rates are available.", detail);
            },
            cancellationToken);
    }

    private async Task<OperationalHealthComponentDto> RunCheckAsync(
        string key,
        string name,
        bool isCritical,
        string? endpoint,
        int timeoutSeconds,
        Func<CancellationToken, Task<HealthCheckResult>> check,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            var result = await check(timeout.Token);
            started.Stop();
            return BuildComponent(key, name, isCritical, endpoint, result, started.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            started.Stop();
            return BuildComponent(
                key,
                name,
                isCritical,
                endpoint,
                new HealthCheckFailure($"Health check timed out after {timeoutSeconds} seconds.", null),
                started.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            started.Stop();
            return BuildComponent(
                key,
                name,
                isCritical,
                endpoint,
                new HealthCheckFailure("Health check failed.", $"{ex.GetType().Name}: {ex.Message}"),
                started.ElapsedMilliseconds);
        }
    }

    private static OperationalHealthComponentDto BuildComponent(
        string key,
        string name,
        bool isCritical,
        string? endpoint,
        HealthCheckResult result,
        long durationMs)
    {
        return new OperationalHealthComponentDto
        {
            Key = key,
            Name = name,
            IsCritical = isCritical,
            Endpoint = MaskEndpoint(endpoint),
            Status = result.Status,
            Message = result.Message,
            Detail = result.Detail,
            DurationMs = durationMs,
            CheckedAt = MongoliaClock.Now
        };
    }

    private static string BuildOverallStatus(IReadOnlyCollection<OperationalHealthComponentDto> components)
    {
        if (components.Any(component => component.IsCritical && component.Status == HealthStatusNames.Unhealthy))
        {
            return HealthStatusNames.Unhealthy;
        }

        if (components.Any(component => component.Status is HealthStatusNames.Unhealthy or HealthStatusNames.Warning))
        {
            return HealthStatusNames.Degraded;
        }

        return HealthStatusNames.Healthy;
    }

    private string BuildAiServiceBaseUrl()
    {
        var baseUrl = _configuration["AiService:BaseUrl"] ?? "http://localhost:8000";
        return baseUrl.TrimEnd('/') + "/";
    }

    private static string? MaskEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return endpoint;
        }

        return endpoint.Contains("Password=", StringComparison.OrdinalIgnoreCase)
               || endpoint.Contains("Server=", StringComparison.OrdinalIgnoreCase)
               || endpoint.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
            ? "Connection string configured"
            : endpoint;
    }

    private static string FormatDuration(TimeSpan value)
    {
        return value.TotalDays >= 1
            ? $"{value.Days}d {value.Hours}h {value.Minutes}m"
            : $"{value.Hours}h {value.Minutes}m {value.Seconds}s";
    }

    private abstract record HealthCheckResult(string Status, string Message, string? Detail);
    private sealed record HealthCheckSuccess(string Message, string? Detail) : HealthCheckResult(HealthStatusNames.Healthy, Message, Detail);
    private sealed record HealthCheckWarning(string Message, string? Detail) : HealthCheckResult(HealthStatusNames.Warning, Message, Detail);
    private sealed record HealthCheckFailure(string Message, string? Detail) : HealthCheckResult(HealthStatusNames.Unhealthy, Message, Detail);
    private sealed record GeminiHealthResponse(
        bool KeyConfigured,
        bool ApiReachable,
        bool ModelAvailable,
        string? ModelName,
        string? BaseUrl,
        string? Message);
}
