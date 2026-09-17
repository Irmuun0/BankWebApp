using BankWebApp.Web.DTOs.Admin;
using Microsoft.AspNetCore.Components;

namespace BankWebApp.Web.Components.Pages;

// AdminAccounts.razor нь дэлгэц; энэ partial class нь төлөв, event, өгөгдөл ачаалах логик.
public partial class AdminAccounts
{
    private AdminPagedResultDto<AdminAccountDto> accounts = new();
    private AdminAccountLimitDetailsDto? limitDetails;
    private bool isLoading = true;

    [SupplyParameterFromQuery] public string? Search { get; set; }
    [SupplyParameterFromQuery] public string? Username { get; set; }
    [SupplyParameterFromQuery] public string? AccountNumber { get; set; }
    [SupplyParameterFromQuery] public string? Currency { get; set; }
    [SupplyParameterFromQuery] public string? Status { get; set; }
    [SupplyParameterFromQuery] public decimal? MinBalance { get; set; }
    [SupplyParameterFromQuery] public decimal? MaxBalance { get; set; }
    [SupplyParameterFromQuery] public DateOnly? CreatedFrom { get; set; }
    [SupplyParameterFromQuery] public DateOnly? CreatedTo { get; set; }
    [SupplyParameterFromQuery] public int Page { get; set; } = 1;
    [SupplyParameterFromQuery] public int PageSize { get; set; } = 20;
    [SupplyParameterFromQuery] public string? Success { get; set; }
    [SupplyParameterFromQuery] public string? Error { get; set; }
    [SupplyParameterFromQuery] public long? LimitAccountId { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        isLoading = true;
        Page = Math.Max(Page, 1);
        PageSize = NormalizePageSize(PageSize);

        var filter = new AdminAccountFilterDto
        {
            Search = Search,
            Username = Username,
            AccountNumber = AccountNumber,
            Currency = Currency,
            Status = Status,
            MinBalance = MinBalance,
            MaxBalance = MaxBalance,
            CreatedFrom = CreatedFrom,
            CreatedTo = CreatedTo
        };

        accounts = await AdminService.GetAccountsAsync(filter, Page, PageSize);
        if (LimitAccountId is not null)
        {
            limitDetails = await AdminService.GetAccountLimitDetailsAsync(LimitAccountId.Value);
        }

        isLoading = false;
    }

    private string CurrentPageUrl => "/" + Navigation.ToBaseRelativePath(Navigation.Uri).Split('#')[0];

    private string CloseLimitModalUrl => BuildUrl(includeLimitId: false);

    private string BuildLimitModalUrl(long accountId)
    {
        return BuildUrl(includeLimitId: true, accountId);
    }

    private string BuildUrl(bool includeLimitId, long? accountId = null)
    {
        var query = BuildPagerQuery().ToDictionary();
        query["page"] = Page.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (includeLimitId && accountId is long id)
        {
            query["limitAccountId"] = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return query.Count == 0 ? "/admin/accounts" : $"/admin/accounts?{string.Join("&", query.Select(item => $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value ?? "")}"))}";
    }

    private IReadOnlyDictionary<string, string?> BuildPagerQuery()
    {
        return new Dictionary<string, string?>
        {
            ["search"] = Search,
            ["username"] = Username,
            ["accountNumber"] = AccountNumber,
            ["currency"] = Currency,
            ["status"] = Status,
            ["minBalance"] = FormatDecimal(MinBalance),
            ["maxBalance"] = FormatDecimal(MaxBalance),
            ["createdFrom"] = FormatDateInput(CreatedFrom),
            ["createdTo"] = FormatDateInput(CreatedTo),
            ["pageSize"] = PageSize.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    private static string FormatLimitValue(decimal value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    private static string FormatHistoryLimit(decimal? value) => value is decimal amount ? $"{amount:N2} MNT" : "-";
    private static string? FormatDecimal(decimal? value) => value?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    private static string? FormatDateInput(DateOnly? value) => value?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    private static int NormalizePageSize(int value) => value is 50 or 100 ? value : 20;
    private static string DisplayAccountType(string accountType) => accountType == "CHECKING" ? "Харилцах" : accountType;
    private static string DisplayActive(bool isActive) => isActive ? "Идэвхтэй" : "Идэвхгүй";
}
