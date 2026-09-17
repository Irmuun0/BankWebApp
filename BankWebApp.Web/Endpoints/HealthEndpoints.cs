using BankWebApp.Web.Services.Interfaces;
using BankWebApp.Web.DTOs.Health;
using static BankWebApp.Web.Endpoints.EndpointHelpers;

namespace BankWebApp.Web.Endpoints;

internal static class HealthEndpoints
{
    // Endpoint нь form-ыг DTO болгож service рүү дамжуулна; бизнес дүрэм service-д байна.
    internal static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", async (IOperationalHealthService healthService, CancellationToken cancellationToken) =>
        {
            var report = await healthService.CheckAsync(cancellationToken);
            var statusCode = report.Status == HealthStatusNames.Unhealthy
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status200OK;

            return Results.Json(report, statusCode: statusCode);
        });
    }
}
