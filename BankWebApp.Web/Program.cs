using BankWebApp.Web.Components;
using BankWebApp.Web.Configuration;
using BankWebApp.Web.Endpoints;
using BankWebApp.Web.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.AddBankServices();

var app = builder.Build();

// Middleware-ийн дараалал чухал: session → cookie → хэрэглэгчийн төлөв → эрх → antiforgery.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // HTTPS хамгаалалтыг өмнөхтэй адил Development-ээс бусад орчинд идэвхжүүлнэ.
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseSession();
app.UseAuthentication();
app.UseProtectedPages();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();

// URL, form талбар, authorization-ыг тухайн endpoint файлд бүртгэнэ.
app.MapRegistrationEndpoints();
app.MapAuthenticationEndpoints();
app.MapProfileEndpoints();
app.MapAccountEndpoints();
app.MapAdminUserEndpoints();
app.MapAdminAccountEndpoints();
app.MapFraudReviewEndpoints();
app.MapAiDetectionEndpoints();
app.MapFraudRuleEndpoints();
app.MapExchangeRateEndpoints();
app.MapTransactionEndpoints();
app.MapHealthEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
