using BankWebApp.Web.Services.Interfaces;
using BankWebApp.Web.DTOs.Admin;
using System.Globalization;
using static BankWebApp.Web.Endpoints.EndpointHelpers;

namespace BankWebApp.Web.Endpoints;

internal static class AdminAccountEndpoints
{
    // Endpoint нь form-ыг DTO болгож service рүү дамжуулна; бизнес дүрэм service-д байна.
    internal static void MapAdminAccountEndpoints(this WebApplication app)
    {
        app.MapPost("/admin/accounts/toggle-status", async (HttpContext context, IAdminService adminService, CancellationToken cancellationToken) =>
        {
            if (!TryReadCurrentUserId(context, out var adminUserId))
            {
                return Results.Redirect("/admin/login?error=Та%20нэвтрээгүй%20байна.");
            }

            var form = await context.Request.ReadFormAsync(cancellationToken);
            var returnUrl = GetSafeLocalUrl(form["ReturnUrl"].ToString(), "/admin/accounts");
            if (!long.TryParse(form["AccountId"].ToString(), out var accountId) || !TryReadBoolean(form["IsActive"].ToString(), out var isActive))
            {
                return Results.Redirect(AppendQuery(returnUrl, "error", "Дансны төлөв өөрчлөх хүсэлт буруу байна."));
            }

            var result = await adminService.SetAccountActiveStatusAsync(adminUserId, accountId, isActive, cancellationToken);
            var queryName = result.Success ? "success" : "error";
            return Results.Redirect(AppendQuery(returnUrl, queryName, result.ErrorMessage ?? "Хүсэлт боловсруулах үед алдаа гарлаа."));
        }).RequireAuthorization(policy => policy.RequireRole("ADMIN"));

        app.MapPost("/admin/accounts/transaction-limit", async (HttpContext context, IAdminService adminService, CancellationToken cancellationToken) =>
        {
            if (!TryReadCurrentUserId(context, out var adminUserId))
            {
                return Results.Redirect("/admin/login?error=Та%20нэвтрээгүй%20байна.");
            }

            var form = await context.Request.ReadFormAsync(cancellationToken);
            var returnUrl = GetSafeLocalUrl(form["ReturnUrl"].ToString(), "/admin/accounts");
            if (!long.TryParse(form["AccountId"].ToString(), out var accountId))
            {
                return Results.Redirect(AppendQuery(returnUrl, "error", "Дансны мэдээлэл буруу байна."));
            }

            decimal dailyLimitMnt;
            var limitValue = form["LimitAmount"].ToString();
            if (string.IsNullOrWhiteSpace(limitValue) ||
                !decimal.TryParse(limitValue, NumberStyles.Number, CultureInfo.InvariantCulture, out dailyLimitMnt))
            {
                return Results.Redirect(AppendQuery(returnUrl, "error", "Өдрийн лимитийн дүн буруу байна."));
            }

            var result = await adminService.UpdateAccountTransactionLimitAsync(
                adminUserId,
                new UpdateAccountTransactionLimitDto
                {
                    AccountId = accountId,
                    DailyLimitMnt = dailyLimitMnt,
                    Reason = form["Reason"].ToString()
                },
                cancellationToken);

            var queryName = result.Success ? "success" : "error";
            return Results.Redirect(AppendQuery(returnUrl, queryName, result.ErrorMessage ?? "Лимит тохируулах үед алдаа гарлаа."));
        }).RequireAuthorization(policy => policy.RequireRole("ADMIN"));
    }
}
