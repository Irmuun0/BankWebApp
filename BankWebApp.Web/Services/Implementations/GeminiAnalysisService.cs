using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BankWebApp.Web.DTOs.Ai;
using BankWebApp.Web.Services.Interfaces;

namespace BankWebApp.Web.Services.Implementations;

public class GeminiAnalysisService : IGeminiAnalysisService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiAnalysisService> _logger;

    public GeminiAnalysisService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GeminiAnalysisService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var baseUrl = configuration["AiService:BaseUrl"] ?? "http://localhost:8000";
        var timeoutSeconds = configuration.GetValue<int?>("AiService:GeminiTimeoutSeconds") ?? 120;
        _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 10, 300));
    }

    public async Task<(bool Success, GeminiTransactionAnalysisResultDto? Result, string? ErrorMessage)> AnalyzeTransactionAsync(
        GeminiSuspiciousAnalysisContextDto context,
        string? modelName = null,
        CancellationToken cancellationToken = default)
    {
        var result = await PostAsync<AnalyzeRequest, GeminiTransactionAnalysisResultDto>(
            "analyze-transaction",
            new AnalyzeRequest(context, NormalizeModelName(modelName)),
            cancellationToken);

        return result.Success
            ? (true, NormalizeResult(result.Value!), null)
            : (false, null, result.ErrorMessage);
    }

    public async Task<(bool Success, string? Analysis, string? ErrorMessage)> AnalyzeSuspiciousTransactionAsync(
        GeminiSuspiciousAnalysisContextDto context,
        CancellationToken cancellationToken = default)
    {
        var result = await PostAsync<ExplainRequest, ExplainResponse>(
            "chat/explain",
            new ExplainRequest(context),
            cancellationToken);

        return result.Success
            ? (true, NormalizeText(result.Value!.Analysis), null)
            : (false, null, result.ErrorMessage);
    }

    public async Task<(bool Success, string? Answer, string? ErrorMessage)> AskTransactionAnalysisQuestionAsync(
        GeminiSuspiciousAnalysisContextDto context,
        string existingAnalysis,
        string question,
        string? modelName = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return (false, null, "Асуулт хоосон байна.");
        }

        var result = await PostAsync<ChatRequest, ChatResponse>(
            "chat/ask",
            new ChatRequest(context, existingAnalysis, question.Trim(), NormalizeModelName(modelName)),
            cancellationToken);

        return result.Success
            ? (true, NormalizeText(result.Value!.Answer), null)
            : (false, null, result.ErrorMessage);
    }

    public async Task<(bool Success, string? Answer, string? ErrorMessage)> AskPublicBankInfoQuestionAsync(
        string question,
        IReadOnlyList<PublicBankChatMessageDto> conversation,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return (false, null, "Асуулт хоосон байна.");
        }

        var safeConversation = conversation
            .Where(message => !string.IsNullOrWhiteSpace(message.Content))
            .TakeLast(10)
            .Select(message => new PublicBankChatMessageDto
            {
                Role = string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user",
                Content = NormalizeText(message.Content)
            })
            .ToList();

        var result = await PostAsync<BankInfoChatRequest, BankInfoChatResponse>(
            "chat/bank-info",
            new BankInfoChatRequest(question.Trim(), safeConversation),
            cancellationToken);

        return result.Success
            ? (true, NormalizeText(result.Value!.Answer), null)
            : (false, null, result.ErrorMessage);
    }

    public async Task<(bool Success, string? Answer, string? ErrorMessage)> AskUserFinanceQuestionAsync(
        string question,
        UserFinanceChatContextDto context,
        IReadOnlyList<PublicBankChatMessageDto> conversation,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return (false, null, "Асуулт хоосон байна.");
        }

        var safeConversation = conversation
            .Where(message => !string.IsNullOrWhiteSpace(message.Content))
            .TakeLast(10)
            .Select(message => new PublicBankChatMessageDto
            {
                Role = string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user",
                Content = NormalizeText(message.Content)
            })
            .ToList();

        var result = await PostAsync<UserFinanceChatRequest, BankInfoChatResponse>(
            "chat/user-finance",
            new UserFinanceChatRequest(question.Trim(), context, safeConversation),
            cancellationToken);

        return result.Success
            ? (true, NormalizeText(result.Value!.Answer), null)
            : (false, null, result.ErrorMessage);
    }

    private async Task<(bool Success, TResponse? Value, string? ErrorMessage)> PostAsync<TRequest, TResponse>(
        string endpoint,
        TRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(endpoint, request, JsonOptions, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AI service request failed. Endpoint: {Endpoint}. Status: {StatusCode}. Body: {Body}",
                    endpoint,
                    response.StatusCode,
                    responseText);

                return (false, default, $"AI service алдаа: HTTP {(int)response.StatusCode} - {ExtractErrorMessage(responseText)}");
            }

            var value = JsonSerializer.Deserialize<TResponse>(responseText, JsonOptions);
            return value is null
                ? (false, default, "AI service хоосон хариу буцаалаа.")
                : (true, value, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "AI service unavailable. Endpoint: {Endpoint}.", endpoint);
            return (false, default, $"AI service рүү холбогдох үед алдаа гарлаа: {ex.GetType().Name} - {ex.Message}");
        }
    }

    private static GeminiTransactionAnalysisResultDto NormalizeResult(GeminiTransactionAnalysisResultDto result)
    {
        result.Explanation = NormalizeText(result.Explanation);
        result.RecommendedAction = string.IsNullOrWhiteSpace(result.RecommendedAction)
            ? null
            : NormalizeText(result.RecommendedAction);
        return result;
    }

    private static string NormalizeText(string value)
    {
        var normalized = value.Trim();
        return normalized.Length <= 4000 ? normalized : normalized[..4000];
    }

    private static string ExtractErrorMessage(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return "хоосон error response.";
        }

        try
        {
            using var document = JsonDocument.Parse(responseText);
            if (document.RootElement.TryGetProperty("detail", out var detail))
            {
                return detail.ValueKind == JsonValueKind.String
                    ? TrimError(detail.GetString())
                    : TrimError(detail.ToString());
            }
        }
        catch (JsonException)
        {
            // Raw response fallback below.
        }

        return TrimError(responseText);
    }

    private static string TrimError(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized.Length <= 500 ? normalized : normalized[..500];
    }

    private static string? NormalizeModelName(string? modelName)
    {
        var normalized = modelName?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private sealed record AnalyzeRequest(GeminiSuspiciousAnalysisContextDto Context, string? ModelName);

    private sealed record ExplainRequest(GeminiSuspiciousAnalysisContextDto Context);

    private sealed record ExplainResponse(string Analysis);

    private sealed record ChatRequest(GeminiSuspiciousAnalysisContextDto Context, string ExistingAnalysis, string Question, string? ModelName);

    private sealed record ChatResponse(string Answer);

    private sealed record BankInfoChatRequest(string Question, IReadOnlyList<PublicBankChatMessageDto> Conversation);

    private sealed record BankInfoChatResponse(string Answer);

    private sealed record UserFinanceChatRequest(string Question, UserFinanceChatContextDto Context, IReadOnlyList<PublicBankChatMessageDto> Conversation);
}
