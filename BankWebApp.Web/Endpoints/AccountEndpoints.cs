using BankWebApp.Web.Services.Interfaces;
using BankWebApp.Web.DTOs.Accounts;
using System.Security.Claims;
using static BankWebApp.Web.Endpoints.EndpointHelpers;

namespace BankWebApp.Web.Endpoints;

internal static class AccountEndpoints
{
    // Endpoint нь form-ыг DTO болгож service рүү дамжуулна; бизнес дүрэм service-д байна.
    internal static void MapAccountEndpoints(this WebApplication app)
    {
        app.MapPost("/accounts/open/submit", async (HttpContext context, IAccountService accountService, CancellationToken cancellationToken) =>
        {
            var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdValue, out var userId))
            {
                return Results.Redirect("/?error=Та%20нэвтрээгүй%20байна.");
            }

            var form = await context.Request.ReadFormAsync(cancellationToken);
            var dto = new CreateAccountDto
            {
                Currency = form["Currency"].ToString()
            };

            var result = await accountService.OpenAccountAsync(userId, dto, cancellationToken);
            if (!result.Success)
            {
                return Results.Redirect($"/accounts/open?error={Uri.EscapeDataString(result.ErrorMessage ?? "Данс нээх үед алдаа гарлаа.")}");
            }

            return Results.Redirect($"/accounts/open?success={Uri.EscapeDataString("Данс амжилттай нээгдлээ.")}&accountId={result.Account!.Id}");
        }).RequireAuthorization();

        app.MapPost("/accounts/toggle-status", async (HttpContext context, IAccountService accountService, CancellationToken cancellationToken) =>
        {
            var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdValue, out var userId))
            {
                return Results.Redirect("/?error=Та%20нэвтрээгүй%20байна.");
            }

            var form = await context.Request.ReadFormAsync(cancellationToken);
            var accountIdValue = form["AccountId"].ToString();
            var nextStatusValue = form["IsActive"].ToString();

            if (!long.TryParse(accountIdValue, out var accountId) || !TryReadBoolean(nextStatusValue, out var isActive))
            {
                return Results.Redirect($"/accounts/settings?error={Uri.EscapeDataString("Дансны төлөв өөрчлөх хүсэлт буруу байна.")}");
            }

            var result = await accountService.SetMyAccountStatusAsync(userId, accountId, isActive, cancellationToken);
            if (!result.Success)
            {
                return Results.Redirect($"/accounts/settings?error={Uri.EscapeDataString(result.ErrorMessage ?? "Дансны төлөв өөрчлөх үед алдаа гарлаа.")}");
            }

            return Results.Redirect($"/accounts/settings?success={Uri.EscapeDataString(result.ErrorMessage ?? "Дансны төлөв шинэчлэгдлээ.")}");
        }).RequireAuthorization();

        app.MapPost("/accounts/set-primary", async (HttpContext context, IAccountService accountService, CancellationToken cancellationToken) =>
        {
            if (!TryReadCurrentUserId(context, out var userId))
            {
                return Results.Redirect("/?error=Та%20нэвтрээгүй%20байна.");
            }

            var form = await context.Request.ReadFormAsync(cancellationToken);
            var returnUrl = GetSafeLocalUrl(form["ReturnUrl"].ToString(), "/accounts");
            if (!long.TryParse(form["AccountId"].ToString(), out var accountId))
            {
                return Results.Redirect(AppendQuery(returnUrl, "error", "Дансны мэдээлэл буруу байна."));
            }

            var result = await accountService.SetPrimaryAccountAsync(userId, accountId, cancellationToken);
            if (!result.Success)
            {
                return Results.Redirect(AppendQuery(returnUrl, "error", result.ErrorMessage ?? "Үндсэн данс тохируулах үед алдаа гарлаа."));
            }

            return Results.Redirect(AppendQuery(returnUrl, "success", result.ErrorMessage ?? "Үндсэн данс шинэчлэгдлээ."));
        }).RequireAuthorization();
    }
}
