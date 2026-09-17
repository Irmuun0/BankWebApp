using BankWebApp.Web.Services.Interfaces;
using static BankWebApp.Web.Endpoints.EndpointHelpers;

namespace BankWebApp.Web.Endpoints;

internal static class AiDetectionEndpoints
{
    // Endpoint нь form-ыг DTO болгож service рүү дамжуулна; бизнес дүрэм service-д байна.
    internal static void MapAiDetectionEndpoints(this WebApplication app)
    {
        app.MapPost("/admin/ai-detection/analyze", async (HttpContext context, IAdminService adminService, CancellationToken cancellationToken) =>
        {
            if (!TryReadCurrentUserId(context, out var adminUserId))
            {
                return Results.Redirect("/admin/login?error=Та%20нэвтрээгүй%20байна.");
            }

            var form = await context.Request.ReadFormAsync(cancellationToken);
            var returnUrl = GetSafeLocalUrl(form["ReturnUrl"].ToString(), "/admin/ai-detection");
            var transactionIds = form["TransactionId"]
                .Select(value => long.TryParse(value, out var id) ? id : 0)
                .Where(id => id > 0)
                .ToList();

            var result = await adminService.AnalyzeTransactionsWithAiAsync(adminUserId, transactionIds, form["ModelName"].ToString(), cancellationToken);
            var queryName = result.Success ? "success" : "error";
            return Results.Redirect(AppendQuery(returnUrl, queryName, result.ErrorMessage ?? "AI Detection ажиллуулах үед алдаа гарлаа."));
        }).RequireAuthorization(policy => policy.RequireRole("ADMIN"));

        app.MapPost("/admin/ai-detection/chat", async (HttpContext context, IAdminService adminService, CancellationToken cancellationToken) =>
        {
            if (!TryReadCurrentUserId(context, out var adminUserId))
            {
                return Results.Redirect("/admin/login?error=Та%20нэвтрээгүй%20байна.");
            }

            var form = await context.Request.ReadFormAsync(cancellationToken);
            var returnUrl = GetSafeLocalUrl(form["ReturnUrl"].ToString(), "/admin/ai-detection");
            if (!long.TryParse(form["TransactionId"].ToString(), out var transactionId))
            {
                return Results.Redirect(AppendQuery(returnUrl, "error", "AI chat transaction буруу байна."));
            }

            var result = await adminService.AskAiDetectionQuestionAsync(
                adminUserId,
                transactionId,
                form["Question"].ToString(),
                form["ModelName"].ToString(),
                cancellationToken);

            var queryName = result.Success ? "success" : "error";
            return Results.Redirect(AppendQuery(returnUrl, queryName, result.ErrorMessage ?? "AI chat ажиллуулах үед алдаа гарлаа."));
        }).RequireAuthorization(policy => policy.RequireRole("ADMIN"));
    }
}
