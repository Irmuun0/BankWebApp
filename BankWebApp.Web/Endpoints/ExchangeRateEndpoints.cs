using BankWebApp.Web.Services.Interfaces;
using BankWebApp.Web.DTOs.Admin;
using static BankWebApp.Web.Endpoints.EndpointHelpers;

namespace BankWebApp.Web.Endpoints;

internal static class ExchangeRateEndpoints
{
    // Endpoint нь form-ыг DTO болгож service рүү дамжуулна; бизнес дүрэм service-д байна.
    internal static void MapExchangeRateEndpoints(this WebApplication app)
    {
        app.MapPost("/admin/exchange-rates/algorithm", async (HttpContext context, IAdminService adminService, CancellationToken cancellationToken) =>
        {
            if (!TryReadCurrentUserId(context, out var adminUserId))
            {
                return Results.Redirect("/admin/login?error=Та%20нэвтрээгүй%20байна.");
            }

            var form = await context.Request.ReadFormAsync(cancellationToken);
            if (!long.TryParse(form["SettingId"].ToString(), out var settingId) ||
                !TryReadDecimal(form["BuyMarginPercent"].ToString(), out var buyMarginPercent) ||
                !TryReadDecimal(form["SellMarginPercent"].ToString(), out var sellMarginPercent))
            {
                return Results.Redirect(AppendQuery("/admin/exchange-rates", "error", "Алгоритмын ханшийн хүсэлт буруу байна."));
            }

            var result = await adminService.UpdateCurrencyRateAlgorithmAsync(
                adminUserId,
                new UpdateCurrencyRateAlgorithmDto
                {
                    SettingId = settingId,
                    BuyMarginPercent = buyMarginPercent,
                    SellMarginPercent = sellMarginPercent
                },
                cancellationToken);

            return Results.Redirect(AppendQuery(
                "/admin/exchange-rates",
                result.Success ? "success" : "error",
                result.ErrorMessage ?? "Хүсэлт боловсруулах үед алдаа гарлаа."));
        }).RequireAuthorization(policy => policy.RequireRole("ADMIN"));

        app.MapPost("/admin/exchange-rates/manual", async (HttpContext context, IAdminService adminService, CancellationToken cancellationToken) =>
        {
            if (!TryReadCurrentUserId(context, out var adminUserId))
            {
                return Results.Redirect("/admin/login?error=Та%20нэвтрээгүй%20байна.");
            }

            var form = await context.Request.ReadFormAsync(cancellationToken);
            if (!long.TryParse(form["SettingId"].ToString(), out var settingId) ||
                !TryReadDecimal(form["BuyAdjustment"].ToString(), out var buyAdjustment) ||
                !TryReadDecimal(form["SellAdjustment"].ToString(), out var sellAdjustment) ||
                !TryReadDateTime(form["StartAt"].ToString(), out var startsAt) ||
                !TryReadDateTime(form["EndAt"].ToString(), out var endsAt))
            {
                return Results.Redirect(AppendQuery("/admin/exchange-rates", "error", "Manual override хүсэлт буруу байна."));
            }

            var result = await adminService.SetManualCurrencyRateOverrideAsync(
                adminUserId,
                new SetManualCurrencyRateOverrideDto
                {
                    SettingId = settingId,
                    AdjustmentMode = form["AdjustmentMode"].ToString(),
                    BuyAdjustment = buyAdjustment,
                    SellAdjustment = sellAdjustment,
                    StartsAt = startsAt,
                    EndsAt = endsAt,
                    Note = form["Note"].ToString()
                },
                cancellationToken);

            return Results.Redirect(AppendQuery(
                "/admin/exchange-rates",
                result.Success ? "success" : "error",
                result.ErrorMessage ?? "Хүсэлт боловсруулах үед алдаа гарлаа."));
        }).RequireAuthorization(policy => policy.RequireRole("ADMIN"));

        app.MapPost("/admin/exchange-rates/disable-manual", async (HttpContext context, IAdminService adminService, CancellationToken cancellationToken) =>
        {
            if (!TryReadCurrentUserId(context, out var adminUserId))
            {
                return Results.Redirect("/admin/login?error=Та%20нэвтрээгүй%20байна.");
            }

            var form = await context.Request.ReadFormAsync(cancellationToken);
            if (!long.TryParse(form["ScheduleId"].ToString(), out var scheduleId))
            {
                return Results.Redirect(AppendQuery("/admin/exchange-rates", "error", "Manual override цуцлах хүсэлт буруу байна."));
            }

            var result = await adminService.CancelCurrencyRateOverrideScheduleAsync(adminUserId, scheduleId, cancellationToken);
            return Results.Redirect(AppendQuery(
                "/admin/exchange-rates",
                result.Success ? "success" : "error",
                result.ErrorMessage ?? "Хүсэлт боловсруулах үед алдаа гарлаа."));
        }).RequireAuthorization(policy => policy.RequireRole("ADMIN"));
    }
}
