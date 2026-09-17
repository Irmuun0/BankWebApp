using BankWebApp.Web.DTOs.Admin;
using BankWebApp.Web.Helpers;
using Microsoft.AspNetCore.Components;

namespace BankWebApp.Web.Components.Pages;

// AdminUserProfile.razor нь дэлгэц; энэ partial class нь төлөв, event, өгөгдөл ачаалах логик.
public partial class AdminUserProfile
{
    [Parameter]
    public long UserId { get; set; }

    [SupplyParameterFromQuery(Name = "success")]
    public string? Success { get; set; }

    [SupplyParameterFromQuery(Name = "error")]
    public string? Error { get; set; }

    private AdminUserProfileDto? profile;
    private bool isLoading = true;

    private string CurrentPageUrl => "/" + Navigation.ToBaseRelativePath(Navigation.Uri).Split('?', '#')[0];

    protected override async Task OnInitializedAsync()
    {
        profile = await AdminService.GetUserProfileAsync(UserId);
        isLoading = false;
    }

    private static string DisplayName(AdminUserProfileDto item)
    {
        return UserDisplayNameFormatter.Format(item.FirstName, item.LastName, item.Username);
    }

    private static string GetInitial(AdminUserProfileDto item)
    {
        var displayName = DisplayName(item);
        return string.IsNullOrWhiteSpace(displayName) ? "U" : displayName[..1].ToUpperInvariant();
    }

    private static string RoleLabel(string role)
    {
        return string.Equals(role, "ADMIN", StringComparison.OrdinalIgnoreCase) ? "Админ" : "Хэрэглэгч";
    }

    private static string StatusLabel(bool isActive)
    {
        return isActive ? "Идэвхтэй" : "Идэвхгүй";
    }

    private static string FormatDate(DateTime? value)
    {
        return value.HasValue ? value.Value.ToString("yyyy-MM-dd HH:mm") : "Бүртгэлгүй";
    }

    private static string DisplayAccountType(string accountType)
    {
        return string.Equals(accountType, "CHECKING", StringComparison.OrdinalIgnoreCase) ? "Харилцах" : accountType;
    }
}
