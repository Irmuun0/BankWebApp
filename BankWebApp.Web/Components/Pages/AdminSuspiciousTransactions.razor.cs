using BankWebApp.Web.DTOs.Admin;
using Microsoft.AspNetCore.Components;

namespace BankWebApp.Web.Components.Pages;

// AdminSuspiciousTransactions.razor нь дэлгэц; энэ partial class нь төлөв, event, өгөгдөл ачаалах логик.
public partial class AdminSuspiciousTransactions
{
    private AdminPagedResultDto<AdminSuspiciousTransactionDto> suspiciousTransactions = new();
    private AdminSuspiciousTransactionDto? selectedDetail;
    private bool isLoading = true;

    [SupplyParameterFromQuery]
    public string? Error { get; set; }

    [SupplyParameterFromQuery]
    public string? Success { get; set; }

    [SupplyParameterFromQuery]
    public string? Search { get; set; }

    [SupplyParameterFromQuery]
    public string? ReviewStatus { get; set; }

    [SupplyParameterFromQuery]
    public int Page { get; set; } = 1;

    [SupplyParameterFromQuery(Name = "detail")]
    private long? DetailTransactionId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        suspiciousTransactions = await AdminService.GetSuspiciousTransactionsAsync(
            new AdminSuspiciousTransactionFilterDto
            {
                Search = Search,
                ReviewStatus = ReviewStatus
            },
            Page);
        if (DetailTransactionId is not null)
        {
            selectedDetail = await AdminService.GetSuspiciousTransactionDetailAsync(DetailTransactionId.Value);
        }

        isLoading = false;
    }

    private string CurrentPageUrl => "/" + Navigation.ToBaseRelativePath(Navigation.Uri);

    private string CurrentPageWithDetail => BuildDetailHref(selectedDetail?.TransactionId ?? DetailTransactionId ?? 0);

    private string CloseModalUrl => BuildListHref();

    private string BuildDetailHref(long transactionId)
    {
        var query = BuildQuery([("detail", transactionId.ToString())]);
        return $"/admin/suspicious-transactions?{query}";
    }

    private string BuildListHref()
    {
        var query = BuildQuery([]);
        return string.IsNullOrWhiteSpace(query) ? "/admin/suspicious-transactions" : $"/admin/suspicious-transactions?{query}";
    }

    private string BuildQuery(IEnumerable<(string Key, string Value)> extra)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(Search))
        {
            query.Add($"search={Uri.EscapeDataString(Search)}");
        }

        if (!string.IsNullOrWhiteSpace(ReviewStatus))
        {
            query.Add($"reviewStatus={Uri.EscapeDataString(ReviewStatus)}");
        }

        if (Page > 1)
        {
            query.Add($"page={Page}");
        }

        query.AddRange(extra.Select(item => $"{item.Key}={Uri.EscapeDataString(item.Value)}"));
        return string.Join("&", query);
    }
}
