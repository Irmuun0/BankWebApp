using BankWebApp.Web.Data;
using Microsoft.EntityFrameworkCore;
using BankWebApp.Web.Services.Interfaces;
using BankWebApp.Web.Services.Implementations;
using BankWebApp.Web.Constants;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using BlazorBootstrap;

namespace BankWebApp.Web.Configuration;

internal static class ServiceRegistration
{
    // DI бүртгэл: service lifetime-уудыг өөрчилбөл Blazor circuit болон DbContext-ийн амьдрах хугацаанд нөлөөлнө.
    internal static void AddBankServices(this WebApplicationBuilder builder)
    {
        var dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys");
        Directory.CreateDirectory(dataProtectionKeysPath);
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
            .SetApplicationName("BankWebApp");

        // Blazor component-уудад нэвтрэлтийн төлөв болон UI санг ашиглуулах бүртгэл.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddBlazorBootstrap();

        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/login";
                options.AccessDeniedPath = "/access-denied";
                options.ExpireTimeSpan = AuthConstants.AdminSessionTimeout;
                options.SlidingExpiration = false;
            });

        builder.Services.AddAuthorization();

        var defaultConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        // DbContext thread-safe биш. Өмнөх transient context / scoped factory lifetime-ыг хадгална.
        builder.Services.AddDbContext<BankDbContext>(options =>
            options.UseSqlServer(defaultConnectionString),
            ServiceLifetime.Transient);
        builder.Services.AddDbContextFactory<BankDbContext>(options =>
            options.UseSqlServer(defaultConnectionString),
            ServiceLifetime.Scoped);

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromMinutes(10);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });
        builder.Services.AddTransient<IAuthService, AuthService>();
        builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
        builder.Services.AddTransient<IAccountService, AccountService>();
        builder.Services.AddTransient<ITransactionService, TransactionService>();
        builder.Services.AddTransient<IAdminService, AdminService>();
        builder.Services.AddTransient<INotificationService, NotificationService>();
        builder.Services.AddTransient<IPasswordPolicyService, PasswordPolicyService>();
        builder.Services.AddTransient<IProfileService, ProfileService>();
        builder.Services.AddTransient<ISecurityEventService, SecurityEventService>();
        builder.Services.AddTransient<IDatabaseClockService, DatabaseClockService>();
        builder.Services.AddTransient<IDatabaseTestService, DatabaseTestService>();
        builder.Services.AddTransient<IOperationalHealthService, OperationalHealthService>();
        builder.Services.AddTransient<IUserRegistrationService, UserRegistrationService>();
        builder.Services.AddScoped<IUiOverlayCoordinator, UiOverlayCoordinator>();
        builder.Services.AddHttpClient<IAiDetectionService, AiDetectionService>();
        builder.Services.AddHttpClient<IGeminiAnalysisService, GeminiAnalysisService>();
        builder.Services.AddHttpClient<IExchangeRateService, MongolBankExchangeRateService>();
    }
}
