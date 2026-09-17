using BankWebApp.Web.DTOs.Admin;
using Microsoft.AspNetCore.Components;

namespace BankWebApp.Web.Components.Pages;

// AdminUsers.razor нь дэлгэц; энэ partial class нь төлөв, event, өгөгдөл ачаалах логик.
public partial class AdminUsers
{
    private AdminPagedResultDto<AdminUserDto> users = new();
    private bool isLoading = true;

    [SupplyParameterFromQuery]
    public string? Search { get; set; }

    [SupplyParameterFromQuery]
    public string? Username { get; set; }

    [SupplyParameterFromQuery]
    public string? Email { get; set; }

    [SupplyParameterFromQuery]
    public string? Name { get; set; }

    [SupplyParameterFromQuery]
    public string? Role { get; set; }

    [SupplyParameterFromQuery]
    public string? Status { get; set; }

    [SupplyParameterFromQuery]
    public DateOnly? CreatedFrom { get; set; }

    [SupplyParameterFromQuery]
    public DateOnly? CreatedTo { get; set; }

    [SupplyParameterFromQuery]
    public int Page { get; set; } = 1;

    [SupplyParameterFromQuery]
    public int PageSize { get; set; } = 20;

    [SupplyParameterFromQuery]
    public string? Success { get; set; }

    [SupplyParameterFromQuery]
    public string? Error { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        isLoading = true;
        Page = Math.Max(Page, 1);
        PageSize = NormalizePageSize(PageSize);

        var filter = new AdminUserFilterDto
        {
            Search = Search,
            Username = Username,
            Email = Email,
            Name = Name,
            Role = Role,
            Status = Status,
            CreatedFrom = CreatedFrom,
            CreatedTo = CreatedTo
        };

        users = await AdminService.GetUsersAsync(filter, Page, PageSize);
        isLoading = false;
    }

    private string BuildPageHref(int page)
    {
        var query = new List<string> { $"page={page}", $"pageSize={NormalizePageSize(PageSize)}" };

        AddQuery(query, "search", Search);
        AddQuery(query, "username", Username);
        AddQuery(query, "email", Email);
        AddQuery(query, "name", Name);
        AddQuery(query, "role", Role);
        AddQuery(query, "status", Status);
        AddQuery(query, "createdFrom", FormatDateInput(CreatedFrom));
        AddQuery(query, "createdTo", FormatDateInput(CreatedTo));

        return $"/admin/users?{string.Join("&", query)}";
    }

    private static void AddQuery(List<string> query, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query.Add($"{key}={Uri.EscapeDataString(value)}");
        }
    }

    private static string? FormatDateInput(DateOnly? value)
    {
        return value?.ToString("yyyy-MM-dd");
    }

    private static int NormalizePageSize(int value)
    {
        return value is 50 or 100 ? value : 20;
    }

    private static string DisplayActive(bool isActive)
    {
        return isActive ? "Идэвхтэй" : "Идэвхгүй";
    }
}
