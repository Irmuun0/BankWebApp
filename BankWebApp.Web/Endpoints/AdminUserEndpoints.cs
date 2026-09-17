using BankWebApp.Web.Services.Interfaces;
using BankWebApp.Web.DTOs.Admin;
using static BankWebApp.Web.Endpoints.EndpointHelpers;

namespace BankWebApp.Web.Endpoints;

internal static class AdminUserEndpoints
{
    // Endpoint нь form-ыг DTO болгож service рүү дамжуулна; бизнес дүрэм service-д байна.
    internal static void MapAdminUserEndpoints(this WebApplication app)
    {
        app.MapPost("/admin/users/toggle-status", async (HttpContext context, IAdminService adminService, CancellationToken cancellationToken) =>
        {
            if (!TryReadCurrentUserId(context, out var adminUserId))
            {
                return Results.Redirect("/admin/login?error=Та%20нэвтрээгүй%20байна.");
            }

            var form = await context.Request.ReadFormAsync(cancellationToken);
            var returnUrl = GetSafeLocalUrl(form["ReturnUrl"].ToString(), "/admin/users");
            if (!long.TryParse(form["UserId"].ToString(), out var userId) || !TryReadBoolean(form["IsActive"].ToString(), out var isActive))
            {
                return Results.Redirect(AppendQuery(returnUrl, "error", "Хэрэглэгчийн төлөв өөрчлөх хүсэлт буруу байна."));
            }

            var result = await adminService.SetUserActiveStatusAsync(adminUserId, userId, isActive, cancellationToken);
            var queryName = result.Success ? "success" : "error";
            return Results.Redirect(AppendQuery(returnUrl, queryName, result.ErrorMessage ?? "Хүсэлт боловсруулах үед алдаа гарлаа."));
        }).RequireAuthorization(policy => policy.RequireRole("ADMIN"));

        app.MapPost("/admin/users/profile/update", async (HttpContext context, IAdminService adminService, CancellationToken cancellationToken) =>
        {
            if (!TryReadCurrentUserId(context, out var adminUserId))
            {
                return Results.Redirect("/admin/login?error=Та%20нэвтрээгүй%20байна.");
            }

            var form = await context.Request.ReadFormAsync(cancellationToken);
            var returnUrl = GetSafeLocalUrl(form["ReturnUrl"].ToString(), "/admin/users");
            if (!long.TryParse(form["UserId"].ToString(), out var userId))
            {
                return Results.Redirect(AppendQuery(returnUrl, "error", "Хэрэглэгчийн мэдээлэл буруу байна."));
            }

            var result = await adminService.UpdateUserProfileAsync(
                adminUserId,
                new UpdateAdminUserProfileDto
                {
                    UserId = userId,
                    Username = form["Username"].ToString(),
                    Email = form["Email"].ToString(),
                    FirstName = form["FirstName"].ToString(),
                    LastName = form["LastName"].ToString(),
                    PhoneNumber = form["PhoneNumber"].ToString(),
                    EmergencyPhoneNumber = form["EmergencyPhoneNumber"].ToString()
                },
                cancellationToken);

            var queryName = result.Success ? "success" : "error";
            return Results.Redirect(AppendQuery(returnUrl, queryName, result.ErrorMessage ?? "Хүсэлт боловсруулах үед алдаа гарлаа."));
        }).RequireAuthorization(policy => policy.RequireRole("ADMIN"));

        app.MapPost("/admin/users/profile/password-reset-required", async (HttpContext context, IAdminService adminService, CancellationToken cancellationToken) =>
        {
            if (!TryReadCurrentUserId(context, out var adminUserId))
            {
                return Results.Redirect("/admin/login?error=Та%20нэвтрээгүй%20байна.");
            }

            var form = await context.Request.ReadFormAsync(cancellationToken);
            var returnUrl = GetSafeLocalUrl(form["ReturnUrl"].ToString(), "/admin/users");
            if (!long.TryParse(form["UserId"].ToString(), out var userId)
                || !TryReadBoolean(form["IsRequired"].ToString(), out var isRequired))
            {
                return Results.Redirect(AppendQuery(returnUrl, "error", "Нууц үг шинэчлэх шаардлагын хүсэлт буруу байна."));
            }

            var result = await adminService.SetUserPasswordResetRequiredAsync(adminUserId, userId, isRequired, cancellationToken);
            var queryName = result.Success ? "success" : "error";
            return Results.Redirect(AppendQuery(returnUrl, queryName, result.ErrorMessage ?? "Хүсэлт боловсруулах үед алдаа гарлаа."));
        }).RequireAuthorization(policy => policy.RequireRole("ADMIN"));

        app.MapPost("/admin/users/profile/unlock", async (HttpContext context, IAdminService adminService, CancellationToken cancellationToken) =>
        {
            if (!TryReadCurrentUserId(context, out var adminUserId))
            {
                return Results.Redirect("/admin/login?error=Та%20нэвтрээгүй%20байна.");
            }

            var form = await context.Request.ReadFormAsync(cancellationToken);
            var returnUrl = GetSafeLocalUrl(form["ReturnUrl"].ToString(), "/admin/users");
            if (!long.TryParse(form["UserId"].ToString(), out var userId))
            {
                return Results.Redirect(AppendQuery(returnUrl, "error", "Хэрэглэгчийн мэдээлэл буруу байна."));
            }

            var result = await adminService.UnlockUserAsync(adminUserId, userId, cancellationToken);
            var queryName = result.Success ? "success" : "error";
            return Results.Redirect(AppendQuery(returnUrl, queryName, result.ErrorMessage ?? "Хүсэлт боловсруулах үед алдаа гарлаа."));
        }).RequireAuthorization(policy => policy.RequireRole("ADMIN"));
    }
}
