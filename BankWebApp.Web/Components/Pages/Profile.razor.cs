using BankWebApp.Web.Helpers;
using Microsoft.AspNetCore.Components;
using BankWebApp.Web.DTOs.Profile;

namespace BankWebApp.Web.Components.Pages;

// Profile.razor нь дэлгэц; энэ partial class нь төлөв, event, өгөгдөл ачаалах логик.
public partial class Profile
{
    [SupplyParameterFromQuery(Name = "success")]
    public string? Success { get; set; }

    [SupplyParameterFromQuery(Name = "error")]
    public string? Error { get; set; }

    private UserProfileDto? profile;
    private bool isLoading = true;

    private string BackHref => string.Equals(profile?.Role, "ADMIN", StringComparison.OrdinalIgnoreCase)
        ? "/admin/dashboard"
        : "/dashboard";

    protected override async Task OnInitializedAsync()
    {
        if (CurrentUser.UserId is null)
        {
            Navigation.NavigateTo("/");
            return;
        }

        profile = await ProfileService.GetProfileAsync(CurrentUser.UserId.Value);
        isLoading = false;
    }

    private static string DisplayName(UserProfileDto item)
    {
        return UserDisplayNameFormatter.Format(item.FirstName, item.LastName, item.Username);
    }

    private static string GetInitial(UserProfileDto item)
    {
        var name = DisplayName(item);
        return string.IsNullOrWhiteSpace(name) ? "U" : name[..1].ToUpperInvariant();
    }

    private static string FormatDate(DateTime? value)
    {
        return value.HasValue ? value.Value.ToString("yyyy-MM-dd HH:mm") : "Бүртгэлгүй";
    }

    private static string RoleLabel(string role)
    {
        return string.Equals(role, "ADMIN", StringComparison.OrdinalIgnoreCase) ? "Админ" : "Хэрэглэгч";
    }
}
