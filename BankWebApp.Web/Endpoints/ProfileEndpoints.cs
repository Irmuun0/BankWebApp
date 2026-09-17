using BankWebApp.Web.Services.Interfaces;
using BankWebApp.Web.Constants;
using BankWebApp.Web.DTOs.Profile;
using BankWebApp.Web.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Globalization;
using System.Security.Claims;
using static BankWebApp.Web.Endpoints.EndpointHelpers;

namespace BankWebApp.Web.Endpoints;

internal static class ProfileEndpoints
{
    // Endpoint нь form-ыг DTO болгож service рүү дамжуулна; бизнес дүрэм service-д байна.
    internal static void MapProfileEndpoints(this WebApplication app)
    {
        app.MapPost("/profile/update", async (HttpContext context, IProfileService profileService, CancellationToken cancellationToken) =>
        {
            if (!TryReadCurrentUserId(context, out var userId))
            {
                return Results.Redirect($"/?error={Uri.EscapeDataString("Та нэвтрээгүй байна.")}");
            }

            var form = await context.Request.ReadFormAsync(cancellationToken);
            var returnUrl = GetSafeLocalUrl(form["ReturnUrl"].ToString(), "/profile");
            var result = await profileService.UpdateProfileAsync(
                userId,
                new UpdateProfileDto
                {
                    Username = form["Username"].ToString(),
                    Email = form["Email"].ToString(),
                    PhoneNumber = form["PhoneNumber"].ToString(),
                    EmergencyPhoneNumber = form["EmergencyPhoneNumber"].ToString(),
                    CurrentPassword = form["CurrentPassword"].ToString(),
                    NewPassword = form["NewPassword"].ToString(),
                    ConfirmPassword = form["ConfirmPassword"].ToString()
                },
                cancellationToken);

            if (!result.Success || result.Profile is null)
            {
                return Results.Redirect(AppendQuery(returnUrl, "error", result.ErrorMessage ?? "Хувийн мэдээлэл шинэчлэх үед алдаа гарлаа."));
            }

            var profile = result.Profile;
            var role = context.User.FindFirstValue(ClaimTypes.Role) ?? profile.Role;
            var expiresUtc = GetCurrentSessionExpiry(context, role);
            var fullName = UserDisplayNameFormatter.Format(profile.FirstName, profile.LastName);
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, profile.UserId.ToString(CultureInfo.InvariantCulture)),
                new(ClaimTypes.Name, profile.Username),
                new(ClaimTypes.Role, role),
                new("FullName", fullName),
                new("PasswordResetRequired", profile.PasswordResetRequired ? "true" : "false"),
                new(AuthConstants.SessionExpiresUtcTicksClaim, expiresUtc.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture))
            };

            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    IssuedUtc = DateTimeOffset.UtcNow,
                    ExpiresUtc = expiresUtc,
                    AllowRefresh = false
                });

            return Results.Redirect(AppendQuery(returnUrl, "success", "Хувийн мэдээлэл шинэчлэгдлээ."));
        }).RequireAuthorization();
    }
}
