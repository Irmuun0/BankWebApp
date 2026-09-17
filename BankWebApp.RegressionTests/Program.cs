using System.Globalization;
using System.Security.Claims;
using BankWebApp.Web.Constants;
using BankWebApp.Web.Endpoints;
using BankWebApp.Web.Helpers;
using BankWebApp.Web.Middleware;
using BankWebApp.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using static BankWebApp.Web.Endpoints.EndpointHelpers;

// DB, Gemini, шинэ NuGet test framework шаардахгүй regression checks.
var checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
    checks++;
}

foreach (var culture in new[] { "en-US", "mn-MN", "de-DE" })
{
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
    foreach (var (input, expected) in new[] { ("0", 0m), ("0.01", .01m), ("12.3", 12.3m), ("001.20", 1.20m), ("9999999999999999.99", 9999999999999999.99m) })
        Check(TransactionInput.TryReadTransactionAmount(input, out var amount) && amount == expected, $"Amount {input}, culture {culture}");
    foreach (var input in new[] { "", " 1", "1 ", "-1", "+1", ".50", "1.", "1.001", "1,20", "1,000", "1e2", "１００", "10000000000000000", "1.2.3" })
        Check(!TransactionInput.TryReadTransactionAmount(input, out _), $"Reject amount {input}, culture {culture}");
}
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
Check(TransactionInput.IsValidAccountNumber("0123456789"), "Leading zero account number");
foreach (var input in new[] { "123456789", "12345678901", "12345 7890", "123456789a", "１２３４５６７８９０" })
    Check(!TransactionInput.IsValidAccountNumber(input), $"Reject account {input}");
Check(Money.TruncateMoney(.995m) == .99m, "Conversion truncates rather than rounds");
Check(Money.TruncateMoney(-.995m) == -.99m, "Negative truncation is toward zero");
Check(Money.TruncateMoney(123456789.999m) == 123456789.99m, "Decimal precision");
Check(Money.TruncateMoney(0m) == 0m, "Zero conversion");

foreach (var url in new[] { "https://example.test", "//example.test", "/\\example.test", "dashboard", "" })
    Check(GetSafeLocalUrl(url, "/dashboard") == "/dashboard", $"Reject nonlocal redirect {url}");
Check(GetSafeLocalUrl("/accounts?search=A%26B", "/") == "/accounts?search=A%26B", "Keep local return URL");
Check(AppendQuery("/profile?tab=edit", "error", "A&B +") == "/profile?tab=edit&error=A%26B%20%2B", "Escape query values");
Check(BuildLoginRedirect("/", "//example.test", "invalid") == "/?error=invalid", "Login rejects remote return URL");
foreach (var input in new[] { "true", "ON", "1", " yes " })
    Check(TryReadBoolean(input, out var value) && value, $"Boolean true {input}");
foreach (var input in new[] { "false", "off", "0", "no" })
    Check(TryReadBoolean(input, out var value) && !value, $"Boolean false {input}");
Check(!TryReadBoolean("maybe", out _), "Reject invalid boolean");
var expiry = DateTimeOffset.UtcNow.AddMinutes(4);
var session = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AuthConstants.SessionExpiresUtcTicksClaim, expiry.UtcTicks.ToString()) }, "test")) };
Check(GetCurrentSessionExpiry(session, "USER") == expiry, "Profile update preserves session expiry");

// Request delegate-уудыг бүтээж route болон authorization metadata-г шалгана.
var builder = WebApplication.CreateBuilder();
builder.Services.AddAuthorization();
foreach (var contract in typeof(IAdminService).Assembly.GetTypes().Where(t => t.IsInterface && t.Namespace == typeof(IAdminService).Namespace))
    builder.Services.AddTransient(contract, _ => throw new InvalidOperationException("Tests must not access external services."));
await using var app = builder.Build();
app.MapRegistrationEndpoints();
app.MapAuthenticationEndpoints();
app.MapProfileEndpoints();
app.MapAccountEndpoints();
app.MapAdminUserEndpoints();
app.MapAdminAccountEndpoints();
app.MapFraudReviewEndpoints();
app.MapAiDetectionEndpoints();
app.MapFraudRuleEndpoints();
app.MapExchangeRateEndpoints();
app.MapTransactionEndpoints();
app.MapHealthEndpoints();
var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(s => s.Endpoints).OfType<RouteEndpoint>().ToList();
var expectedRoutes = new[]
{
    "/registration/request", "/auth/login", "/profile/update", "/accounts/open/submit", "/accounts/toggle-status", "/accounts/set-primary",
    "/admin/users/toggle-status", "/admin/users/profile/update", "/admin/users/profile/password-reset-required", "/admin/users/profile/unlock",
    "/admin/registration-requests/review", "/admin/accounts/toggle-status", "/admin/accounts/transaction-limit",
    "/admin/suspicious-transactions/review", "/admin/suspicious-transactions/escalate", "/admin/ai-detection/analyze", "/admin/ai-detection/chat",
    "/admin/fraud-rules/update", "/admin/fraud-rules/threshold", "/admin/exchange-rates/algorithm", "/admin/exchange-rates/manual", "/admin/exchange-rates/disable-manual",
    "/transactions/create/submit", "/auth/logout", "/health", "/auth/session-expired"
};
Check(endpoints.Count == expectedRoutes.Length, "All 26 HTTP endpoints remain registered");
foreach (var path in expectedRoutes)
{
    var endpoint = endpoints.Single(e => e.RoutePattern.RawText == path);
    var method = path is "/health" or "/auth/session-expired" ? "GET" : "POST";
    Check(endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.SequenceEqual(new[] { method }), $"HTTP method {path}");
    var authorization = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
    var policies = endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>();
    var isPublic = path is "/health" or "/auth/login" or "/auth/session-expired" or "/registration/request";
    Check((authorization.Count + policies.Count > 0) != isPublic, $"Authorization retained {path}");
    if (path.StartsWith("/admin/"))
    {
        var policy = await AuthorizationPolicy.CombineAsync(app.Services.GetRequiredService<IAuthorizationPolicyProvider>(), authorization, policies);
        Check(policy!.Requirements.OfType<Microsoft.AspNetCore.Authorization.Infrastructure.RolesAuthorizationRequirement>().Any(r => r.AllowedRoles.Contains("ADMIN")), $"Admin role {path}");
    }
}

// Middleware-г дангаар ажиллуулна: нэвтрээгүй хүсэлт DB-д хүрэлгүй redirect болох ёстой.
// Extension WebApplication дээр бүртгэгддэг тул шинэ app-д зөвхөн энэ middleware-г угсарна.
await using var protectedApp = WebApplication.CreateBuilder().Build();
protectedApp.UseProtectedPages();
((IApplicationBuilder)protectedApp).Run(context => { context.Response.StatusCode = 204; return Task.CompletedTask; });
var invoke = ((IApplicationBuilder)protectedApp).Build();
foreach (var path in new[] { "/dashboard", "/accounts", "/transactions/create", "/profile", "/admin/dashboard" })
{
    var context = new DefaultHttpContext { RequestServices = protectedApp.Services };
    context.Request.Path = path;
    await invoke(context);
    Check(context.Response.StatusCode == 302, $"Protected page redirect {path}");
    Check(context.Response.Headers.Location.ToString().StartsWith(path.StartsWith("/admin") ? "/?login=admin&" : "/?login=user&"), $"Correct login mode {path}");
}
foreach (var path in new[] { "/", "/admin/login", "/images/logo.png" })
{
    var context = new DefaultHttpContext { RequestServices = protectedApp.Services };
    context.Request.Path = path;
    await invoke(context);
    Check(context.Response.StatusCode == 204, $"Public page remains accessible {path}");
}
foreach (var (path, role, resetRequired, expectedStatus, location) in new[]
{
    ("/admin/dashboard", "USER", false, 302, "/access-denied"),
    ("/admin/dashboard", "ADMIN", false, 204, ""),
    ("/dashboard", "USER", true, 302, "/profile?error="),
    ("/profile", "USER", true, 204, "")
})
{
    // Identity ID байхгүй fixture нь DB lookup алгасаж, role/reset branch-ийг тусад нь шалгана.
    var context = new DefaultHttpContext { RequestServices = protectedApp.Services };
    context.Request.Path = path;
    context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role), new Claim("PasswordResetRequired", resetRequired ? "true" : "false") }, "test"));
    await invoke(context);
    Check(context.Response.StatusCode == expectedStatus, $"Role/reset access {path}, {role}, reset={resetRequired}");
    if (location.Length > 0) Check(context.Response.Headers.Location.ToString().StartsWith(location), $"Role/reset redirect {path}");
}
Console.WriteLine($"PASS: {checks} regression checks (amounts, redirects, session expiry, endpoint roles, protected pages).");
