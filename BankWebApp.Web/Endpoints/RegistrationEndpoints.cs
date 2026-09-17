using BankWebApp.Web.Services.Interfaces;
using BankWebApp.Web.DTOs.Admin;
using BankWebApp.Web.DTOs.Registration;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;
using static BankWebApp.Web.Endpoints.EndpointHelpers;

namespace BankWebApp.Web.Endpoints;

internal static class RegistrationEndpoints
{
    // Endpoint нь form-ыг DTO болгож service рүү дамжуулна; бизнес дүрэм service-д байна.
    internal static void MapRegistrationEndpoints(this WebApplication app)
    {
        app.MapPost("/registration/request", async (HttpContext context, IUserRegistrationService registrationService, CancellationToken cancellationToken) =>
        {
            var form = await context.Request.ReadFormAsync(cancellationToken);
            var result = await registrationService.SubmitRequestAsync(
                new CreateRegistrationRequestDto
                {
                    FirstName = form["FirstName"].ToString(),
                    LastName = form["LastName"].ToString(),
                    RequestedUsername = form["RequestedUsername"].ToString(),
                    Email = form["Email"].ToString(),
                    PhoneNumber = form["PhoneNumber"].ToString(),
                    NationalId = form["NationalId"].ToString(),
                    EmergencyPhoneNumber = form["EmergencyPhoneNumber"].ToString(),
                    PreferredContactMethod = form["PreferredContactMethod"].ToString(),
                    RequestNote = form["RequestNote"].ToString()
                },
                cancellationToken);

            var queryName = result.Success ? "registrationSuccess" : "registrationError";
            return Results.Redirect(AppendQuery("/", queryName, result.ErrorMessage ?? "Registration request processed."));
        }).AllowAnonymous();

        app.MapPost("/admin/registration-requests/review", async (HttpContext context, IUserRegistrationService registrationService, CancellationToken cancellationToken) =>
        {
            if (!TryReadCurrentUserId(context, out var adminUserId))
            {
                return Results.Redirect("/admin/login?error=Not%20signed%20in.");
            }

            var form = await context.Request.ReadFormAsync(cancellationToken);
            var returnUrl = GetSafeLocalUrl(form["ReturnUrl"].ToString(), "/admin/registration-requests");
            if (!long.TryParse(form["RequestId"].ToString(), out var requestId))
            {
                return Results.Redirect(AppendQuery(returnUrl, "error", "Registration request is invalid."));
            }

            var result = await registrationService.ReviewRequestAsync(
                adminUserId,
                new ReviewRegistrationRequestDto
                {
                    RequestId = requestId,
                    Decision = form["Decision"].ToString(),
                    AdminNote = form["AdminNote"].ToString(),
                    DecisionMessage = form["DecisionMessage"].ToString()
                },
                cancellationToken);

            var queryName = result.Success ? "success" : "error";
            var redirectUrl = AppendQuery(returnUrl, queryName, result.Message ?? "Registration request processed.");
            if (result.Success &&
                !string.IsNullOrWhiteSpace(result.TemporaryPassword) &&
                !string.IsNullOrWhiteSpace(result.Username))
            {
                var credentialKey = $"registration-credential:{Guid.NewGuid():N}";
                var credential = new OneTimeRegistrationCredentialDto
                {
                    RequestId = requestId,
                    Username = result.Username,
                    TemporaryPassword = result.TemporaryPassword
                };
                context.Session.SetString(credentialKey, JsonSerializer.Serialize(credential));
                redirectUrl = AppendQuery(redirectUrl, "credentialKey", credentialKey);
            }

            return Results.Redirect(redirectUrl);
        }).RequireAuthorization(policy => policy.RequireRole("ADMIN"));
    }
}
