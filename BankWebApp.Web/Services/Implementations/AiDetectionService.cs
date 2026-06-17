using System.Net.Http.Json;
using BankWebApp.Web.DTOs.Ai;
using BankWebApp.Web.Services.Interfaces;

namespace BankWebApp.Web.Services.Implementations;

public class AiDetectionService : IAiDetectionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AiDetectionService> _logger;

    public AiDetectionService(HttpClient httpClient, IConfiguration configuration, ILogger<AiDetectionService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var baseUrl = configuration["AiService:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        }

        _httpClient.Timeout = TimeSpan.FromSeconds(5);
    }

    public async Task<SuspiciousDetectionResultDto?> DetectSuspiciousAsync(
        SuspiciousDetectionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("detect-suspicious", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AI suspicious detection returned {StatusCode} for transaction {TransactionId}.",
                    response.StatusCode,
                    request.TransactionId);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<SuspiciousDetectionResultDto>(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "AI suspicious detection unavailable for transaction {TransactionId}.", request.TransactionId);
            return null;
        }
    }
}
