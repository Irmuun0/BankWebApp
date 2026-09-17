using BankWebApp.Web.Services.Interfaces;
using BankWebApp.Web.DTOs.Admin;
using static BankWebApp.Web.Endpoints.EndpointHelpers;

namespace BankWebApp.Web.Endpoints;

internal static class FraudRuleEndpoints
{
    // Endpoint нь form-ыг DTO болгож service рүү дамжуулна; бизнес дүрэм service-д байна.
    internal static void MapFraudRuleEndpoints(this WebApplication app)
    {
        app.MapPost("/admin/fraud-rules/update", async (HttpContext context, IAdminService adminService, CancellationToken cancellationToken) =>
        {
            if (!TryReadCurrentUserId(context, out var adminUserId))
            {
                return Results.Redirect("/admin/login?error=Та%20нэвтрээгүй%20байна.");
            }

            var form = await context.Request.ReadFormAsync(cancellationToken);
            if (!int.TryParse(form["Id"].ToString(), out var id) ||
                !int.TryParse(form["Score"].ToString(), out var score))
            {
                return Results.Redirect(AppendQuery("/admin/fraud-rules", "error", "Rule тохиргооны хүсэлт буруу байна."));
            }

            var dto = new UpdateFraudRuleSettingDto
            {
                Id = id,
                IsEnabled = TryReadBoolean(form["IsEnabled"].ToString(), out var enabled) && enabled,
                Score = score,
                NumericThreshold = TryReadNullableDecimal(form["NumericThreshold"].ToString()),
                AmountThresholdMnt = TryReadNullableDecimal(form["AmountThresholdMnt"].ToString()),
                AmountThresholdUsd = TryReadNullableDecimal(form["AmountThresholdUsd"].ToString())
            };

            var result = await adminService.UpdateFraudRuleSettingAsync(adminUserId, dto, cancellationToken);
            return Results.Redirect(AppendQuery(
                "/admin/fraud-rules",
                result.Success ? "success" : "error",
                result.ErrorMessage ?? "Rule тохиргоо шинэчлэх үед алдаа гарлаа."));
        }).RequireAuthorization(policy => policy.RequireRole("ADMIN"));

        app.MapPost("/admin/fraud-rules/threshold", async (HttpContext context, IAdminService adminService, CancellationToken cancellationToken) =>
        {
            if (!TryReadCurrentUserId(context, out var adminUserId))
            {
                return Results.Redirect("/admin/login?error=Та%20нэвтрээгүй%20байна.");
            }

            var form = await context.Request.ReadFormAsync(cancellationToken);
            if (!int.TryParse(form["SuspiciousThreshold"].ToString(), out var suspiciousThreshold))
            {
                return Results.Redirect(AppendQuery("/admin/fraud-rules", "error", "Сэжигтэй босго буруу байна."));
            }

            var result = await adminService.UpdateFraudDetectionSettingsAsync(
                adminUserId,
                new UpdateFraudDetectionSettingsDto { SuspiciousThreshold = suspiciousThreshold },
                cancellationToken);

            return Results.Redirect(AppendQuery(
                "/admin/fraud-rules",
                result.Success ? "success" : "error",
                result.ErrorMessage ?? "Сэжигтэй босго шинэчлэх үед алдаа гарлаа."));
        }).RequireAuthorization(policy => policy.RequireRole("ADMIN"));
    }
}
