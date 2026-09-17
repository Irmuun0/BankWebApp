using BankWebApp.Web.Services.Interfaces;
using BankWebApp.Web.Constants;
using BankWebApp.Web.DTOs.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Globalization;
using System.Security.Claims;
using static BankWebApp.Web.Endpoints.EndpointHelpers;

namespace BankWebApp.Web.Endpoints;

internal static class AuthenticationEndpoints
{
    // Endpoint нь form-ыг DTO болгож service рүү дамжуулна; бизнес дүрэм service-д байна.
    internal static void MapAuthenticationEndpoints(this WebApplication app)
    {
        app.MapPost("/auth/login", async (HttpContext context, IAuthService authService, CancellationToken cancellationToken) =>
        {
            var form = await context.Request.ReadFormAsync(cancellationToken);
            var returnUrl = form["ReturnUrl"].ToString();
            var requiredRole = form["RequiredRole"].ToString();
            var loginPath = GetSafeLocalUrl(form["LoginPath"].ToString(), "/");
            var request = new LoginRequestDto
            {
                UsernameOrEmail = form["UsernameOrEmail"].ToString(),
                Password = form["Password"].ToString()
            };

            var result = await authService.LoginAsync(request, cancellationToken);
            if (!result.Success)
            {
                return Results.Redirect(BuildLoginRedirect(loginPath, returnUrl, result.ErrorMessage ?? "Нэвтрэхэд алдаа гарлаа"));
            }

            if (!string.IsNullOrWhiteSpace(requiredRole)
                && !string.Equals(result.Role, requiredRole, StringComparison.OrdinalIgnoreCase))
            {
                return Results.Redirect(BuildLoginRedirect(loginPath, returnUrl, "Энэ хэсэгт нэвтрэх эрхгүй байна"));
            }

            var sessionTimeout = string.Equals(result.Role, "ADMIN", StringComparison.OrdinalIgnoreCase)
                ? AuthConstants.AdminSessionTimeout
                : AuthConstants.UserSessionTimeout;
            var expiresUtc = DateTimeOffset.UtcNow.Add(sessionTimeout);
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, result.UserId!.Value.ToString()),
                new(ClaimTypes.Name, result.Username ?? string.Empty),
                new(ClaimTypes.Role, result.Role ?? "USER"),
                new("FullName", result.FullName ?? string.Empty),
                new("PasswordResetRequired", result.PasswordResetRequired ? "true" : "false"),
                new(AuthConstants.SessionExpiresUtcTicksClaim, expiresUtc.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture))
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    IssuedUtc = DateTimeOffset.UtcNow,
                    ExpiresUtc = expiresUtc,
                    AllowRefresh = false
                });

            var fallbackUrl = string.Equals(result.Role, "ADMIN", StringComparison.OrdinalIgnoreCase)
                ? "/admin/dashboard"
                : "/dashboard";

            if (result.PasswordResetRequired)
            {
                return Results.Redirect(AppendQuery("/profile", "error", "Нууц үгээ шинэчлэх шаардлагатай байна."));
            }

            return Results.Redirect(GetSafeLocalUrl(returnUrl, fallbackUrl));
        });

        app.MapPost("/auth/logout", async (HttpContext context, ISecurityEventService securityEventService, CancellationToken cancellationToken) =>
        {
            long? userId = null;
            if (long.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId))
            {
                userId = parsedUserId;
            }

            await securityEventService.LogAsync(
                userId,
                context.User.Identity?.Name,
                "LOGOUT",
                true,
                "User logged out.",
                cancellationToken);

            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/");
        }).RequireAuthorization();

        app.MapGet("/auth/session-expired", async (HttpContext context, ISecurityEventService securityEventService, CancellationToken cancellationToken) =>
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                long? userId = null;
                if (long.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId))
                {
                    userId = parsedUserId;
                }

                await securityEventService.LogAsync(
                    userId,
                    context.User.Identity?.Name,
                    "SESSION_EXPIRED",
                    true,
                    "User session expired.",
                    cancellationToken);
            }

            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect($"/?error={Uri.EscapeDataString("Нэвтрэх хугацаа дууссан тул дахин нэвтэрнэ үү.")}");
        });
    }
}
