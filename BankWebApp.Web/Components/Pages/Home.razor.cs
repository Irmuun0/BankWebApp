using Microsoft.AspNetCore.Components;

namespace BankWebApp.Web.Components.Pages;

// Home.razor нь дэлгэц; энэ partial class нь төлөв, event, өгөгдөл ачаалах логик.
public partial class Home
{
    [SupplyParameterFromQuery]
    public string? Error { get; set; }

    [SupplyParameterFromQuery]
    public string? ReturnUrl { get; set; }

    [SupplyParameterFromQuery(Name = "login")]
    public string? LoginMode { get; set; }

    [SupplyParameterFromQuery(Name = "registrationSuccess")]
    public string? RegistrationSuccess { get; set; }

    [SupplyParameterFromQuery(Name = "registrationError")]
    public string? RegistrationError { get; set; }

    private string ReturnUrlOrDashboard => string.IsNullOrWhiteSpace(ReturnUrl) ? "/dashboard" : ReturnUrl;

    private string ReturnUrlOrAdminDashboard => string.IsNullOrWhiteSpace(ReturnUrl) ? "/admin/dashboard" : ReturnUrl;
}
