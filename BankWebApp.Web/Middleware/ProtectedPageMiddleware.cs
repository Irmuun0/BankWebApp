using BankWebApp.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using static BankWebApp.Web.Endpoints.EndpointHelpers;

namespace BankWebApp.Web.Middleware;

internal static class ProtectedPageMiddleware
{
    // Cookie-г уншсаны дараа хэрэглэгчийн одоогийн төлөв, нууц үг шинэчлэх шаардлагыг шалгана.
    internal static void UseProtectedPages(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path;
            var isAdminPath = path.StartsWithSegments("/admin");
            var isAdminLoginPath = path.StartsWithSegments("/admin/login");
            var isUserPath = path.StartsWithSegments("/dashboard");
            var isAccountPath = path.StartsWithSegments("/accounts");
            var isTransactionPath = path.StartsWithSegments("/transactions");
            var isProfilePath = path.StartsWithSegments("/profile");
            var isProtectedPath = (isAdminPath && !isAdminLoginPath) || isUserPath || isAccountPath || isTransactionPath || isProfilePath;
            var passwordResetRequired = false;

            if (isProtectedPath && context.User.Identity?.IsAuthenticated != true)
            {
                var returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
                var loginPath = isAdminPath ? "/?login=admin" : "/?login=user";
                context.Response.Redirect(AppendQuery(loginPath, "returnUrl", returnUrl));
                return;
            }

            if (isProtectedPath && context.User.Identity?.IsAuthenticated == true)
            {
                var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (long.TryParse(userIdValue, out var signedInUserId))
                {
                    var dbContext = context.RequestServices.GetRequiredService<BankDbContext>();
                    var userAccessState = await dbContext.Users
                        .AsNoTracking()
                        .Where(user => user.Id == signedInUserId)
                        .Select(user => new { user.IsActive, user.PasswordResetRequired })
                        .FirstOrDefaultAsync();

                    if (userAccessState is null || !userAccessState.IsActive)
                    {
                        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        var loginMode = isAdminPath ? "admin" : "user";
                        context.Response.Redirect($"/?login={loginMode}&error=Таны нэвтрэх эрх идэвхгүй болсон байна.");
                        return;
                    }

                    passwordResetRequired = userAccessState.PasswordResetRequired;
                }
            }

            if (isProtectedPath
                && context.User.Identity?.IsAuthenticated == true
                && (passwordResetRequired || string.Equals(context.User.FindFirstValue("PasswordResetRequired"), "true", StringComparison.OrdinalIgnoreCase))
                && !IsPasswordResetAllowedPath(path))
            {
                context.Response.Redirect(AppendQuery("/profile", "error", "Нууц үгээ шинэчлэх шаардлагатай байна."));
                return;
            }

            if (isAdminPath && !isAdminLoginPath && !context.User.IsInRole("ADMIN"))
            {
                context.Response.Redirect("/access-denied");
                return;
            }

            await next();
        });
    }
}
