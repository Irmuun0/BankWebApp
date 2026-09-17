using BankWebApp.Web.DTOs.Admin;
using BankWebApp.Web.Helpers;
using Microsoft.AspNetCore.Components;

namespace BankWebApp.Web.Components.Pages;

// AdminAiDetection.razor нь дэлгэц; энэ partial class нь төлөв, event, өгөгдөл ачаалах логик.
public partial class AdminAiDetection
{
    private const string DefaultAiModel = "gemini-3.1-flash-lite";
    private AdminAiDetectionPageDto pageModel = new();
    private bool isLoading = true;

    [SupplyParameterFromQuery]
    public string? Search { get; set; }

    [SupplyParameterFromQuery]
    public string? Username { get; set; }

    [SupplyParameterFromQuery]
    public string? Currency { get; set; }

    [SupplyParameterFromQuery]
    public decimal? MinRiskScore { get; set; }

    [SupplyParameterFromQuery]
    public DateOnly? StartDate { get; set; }

    [SupplyParameterFromQuery]
    public DateOnly? EndDate { get; set; }

    [SupplyParameterFromQuery]
    public long? ChatTransactionId { get; set; }

    [SupplyParameterFromQuery]
    public int Page { get; set; } = 1;

    [SupplyParameterFromQuery]
    public int PageSize { get; set; } = 50;

    [SupplyParameterFromQuery]
    public string? Success { get; set; }

    [SupplyParameterFromQuery]
    public string? Error { get; set; }

    private string CurrentListUrl => BuildPageHref(Page, includeChat: false);

    protected override async Task OnParametersSetAsync()
    {
        pageModel = await AdminService.GetAiDetectionPageAsync(
            Search,
            Username,
            Currency,
            MinRiskScore,
            StartDate,
            EndDate,
            ChatTransactionId,
            Page,
            PageSize);

        isLoading = false;
    }

    private string BuildPresetHref(string preset)
    {
        var today = MongoliaClock.Today;
        return preset switch
        {
            "yesterday" => $"/admin/ai-detection?startDate={today.AddDays(-1):yyyy-MM-dd}&endDate={today.AddDays(-1):yyyy-MM-dd}&pageSize={NormalizedPageSize}",
            "last7" => $"/admin/ai-detection?startDate={today.AddDays(-6):yyyy-MM-dd}&endDate={today:yyyy-MM-dd}&pageSize={NormalizedPageSize}",
            "highRisk" => $"/admin/ai-detection?minRiskScore=60&pageSize={NormalizedPageSize}",
            _ => "/admin/ai-detection"
        };
    }

    private string BuildChatHref(long transactionId)
    {
        return BuildChatReturnUrl(transactionId);
    }

    private string BuildChatReturnUrl(long transactionId)
    {
        return $"{BuildPageHref(Page, includeChat: false)}&chatTransactionId={transactionId}";
    }

    private string BuildPageHref(int page, bool includeChat = true)
    {
        var query = new List<string>
        {
            $"page={Math.Max(1, page)}",
            $"pageSize={NormalizedPageSize}"
        };

        AddQuery(query, "search", Search);
        AddQuery(query, "username", Username);
        AddQuery(query, "currency", Currency);
        AddQuery(query, "minRiskScore", MinRiskScore?.ToString());
        AddQuery(query, "startDate", StartDate?.ToString("yyyy-MM-dd"));
        AddQuery(query, "endDate", EndDate?.ToString("yyyy-MM-dd"));

        if (includeChat && ChatTransactionId is not null)
        {
            AddQuery(query, "chatTransactionId", ChatTransactionId.Value.ToString());
        }

        return $"/admin/ai-detection?{string.Join("&", query)}";
    }

    private int NormalizedPageSize => PageSize == 100 ? 100 : 50;

    private static void AddQuery(List<string> query, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
        }
    }

    private static string DisplayAiText(string? value)
    {
        return value ?? string.Empty;
    }
}
