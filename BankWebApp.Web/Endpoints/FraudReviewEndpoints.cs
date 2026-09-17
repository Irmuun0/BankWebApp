using BankWebApp.Web.Services.Interfaces;
using BankWebApp.Web.DTOs.Admin;
using static BankWebApp.Web.Endpoints.EndpointHelpers;

namespace BankWebApp.Web.Endpoints;

internal static class FraudReviewEndpoints
{
    // Endpoint нь form-ыг DTO болгож service рүү дамжуулна; бизнес дүрэм service-д байна.
    internal static void MapFraudReviewEndpoints(this WebApplication app)
    {
        app.MapPost("/admin/suspicious-transactions/review", async (HttpContext context, IAdminService adminService, CancellationToken cancellationToken) =>
        {
            if (!TryReadCurrentUserId(context, out var adminUserId))
            {
                return Results.Redirect("/admin/login?error=Та%20нэвтрээгүй%20байна.");
            }

            var form = await context.Request.ReadFormAsync(cancellationToken);
            var returnUrl = GetSafeLocalUrl(form["ReturnUrl"].ToString(), "/admin/suspicious-transactions");
            if (!long.TryParse(form["TransactionId"].ToString(), out var transactionId))
            {
                return Results.Redirect(AppendQuery(returnUrl, "error", "Review шинэчлэх хүсэлт буруу байна."));
            }

            long? expectedUpdatedAtTicks = null;
            if (long.TryParse(form["ExpectedUpdatedAtTicks"].ToString(), out var parsedExpectedUpdatedAtTicks))
            {
                expectedUpdatedAtTicks = parsedExpectedUpdatedAtTicks;
            }

            var result = await adminService.UpdateSuspiciousReviewAsync(
                adminUserId,
                new UpdateSuspiciousReviewDto
                {
                    TransactionId = transactionId,
                    ReviewStatus = form["ReviewStatus"].ToString(),
                    ReviewNote = form["ReviewNote"].ToString(),
                    SendUserNotification = form.ContainsKey("SendUserNotification"),
                    UserNotificationMessage = form["UserNotificationMessage"].ToString(),
                    NotifySender = form.ContainsKey("NotifySender"),
                    NotifyReceiver = form.ContainsKey("NotifyReceiver"),
                    SenderNotificationMessage = form["SenderNotificationMessage"].ToString(),
                    ReceiverNotificationMessage = form["ReceiverNotificationMessage"].ToString(),
                    DeactivateSenderAccount = form.ContainsKey("DeactivateSenderAccount"),
                    DeactivateReceiverAccount = form.ContainsKey("DeactivateReceiverAccount"),
                    DeactivateSenderUser = form.ContainsKey("DeactivateSenderUser"),
                    DeactivateReceiverUser = form.ContainsKey("DeactivateReceiverUser"),
                    ExpectedUpdatedAtTicks = expectedUpdatedAtTicks
                },
                cancellationToken);

            var queryName = result.Success ? "success" : "error";
            return Results.Redirect(AppendQuery(returnUrl, queryName, result.ErrorMessage ?? "Review status шинэчлэх үед алдаа гарлаа."));
        }).RequireAuthorization(policy => policy.RequireRole("ADMIN"));

        app.MapPost("/admin/suspicious-transactions/escalate", async (HttpContext context, IAdminService adminService, CancellationToken cancellationToken) =>
        {
            if (!TryReadCurrentUserId(context, out var adminUserId))
            {
                return Results.Redirect("/admin/login?error=Ð¢Ð°%20Ð½ÑÐ²Ñ‚Ñ€ÑÑÐ³Ò¯Ð¹%20Ð±Ð°Ð¹Ð½Ð°.");
            }

            var form = await context.Request.ReadFormAsync(cancellationToken);
            var returnUrl = GetSafeLocalUrl(form["ReturnUrl"].ToString(), "/admin/ai-detection");
            if (!long.TryParse(form["TransactionId"].ToString(), out var transactionId))
            {
                return Results.Redirect(AppendQuery(returnUrl, "error", "Review workflow үүсгэх гүйлгээ буруу байна."));
            }

            var draft = await adminService.GetSuspiciousTransactionDetailAsync(transactionId, cancellationToken);
            if (draft is null)
            {
                return Results.Redirect(AppendQuery(returnUrl, "error", "Review workflow нээх гүйлгээ олдсонгүй."));
            }

            return Results.Redirect($"/admin/suspicious-transactions?detail={transactionId}");
        }).RequireAuthorization(policy => policy.RequireRole("ADMIN"));
    }
}
