using BankWebApp.Web.Components;
using BankWebApp.Web.Data;
using Microsoft.EntityFrameworkCore;
using BankWebApp.Web.Services.Interfaces;
using BankWebApp.Web.Services.Implementations;
using BankWebApp.Web.Constants;
using BankWebApp.Web.DTOs.Auth;
using BankWebApp.Web.DTOs.Accounts;
using BankWebApp.Web.DTOs.Admin;
using BankWebApp.Web.DTOs.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using System.Globalization;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys");
Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("BankWebApp");

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/access-denied";
        options.ExpireTimeSpan = AuthConstants.AdminSessionTimeout;
        options.SlidingExpiration = false;
    });

builder.Services.AddAuthorization();

var defaultConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<BankDbContext>(options =>
    options.UseSqlServer(defaultConnectionString),
    ServiceLifetime.Transient);
builder.Services.AddDbContextFactory<BankDbContext>(options =>
    options.UseSqlServer(defaultConnectionString),
    ServiceLifetime.Scoped);

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<IAuthService, AuthService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddTransient<IAccountService, AccountService>();
builder.Services.AddTransient<ITransactionService, TransactionService>();
builder.Services.AddTransient<IAdminService, AdminService>();
builder.Services.AddTransient<INotificationService, NotificationService>();
builder.Services.AddTransient<IPasswordPolicyService, PasswordPolicyService>();
builder.Services.AddTransient<ISecurityEventService, SecurityEventService>();
builder.Services.AddTransient<IDatabaseClockService, DatabaseClockService>();
builder.Services.AddTransient<IDatabaseTestService, DatabaseTestService>();
builder.Services.AddHttpClient<IAiDetectionService, AiDetectionService>();
builder.Services.AddHttpClient<IGeminiAnalysisService, GeminiAnalysisService>();
builder.Services.AddHttpClient<IExchangeRateService, MongolBankExchangeRateService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAuthentication();
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    var isAdminPath = path.StartsWithSegments("/admin");
    var isAdminLoginPath = path.StartsWithSegments("/admin/login");
    var isUserPath = path.StartsWithSegments("/dashboard");
    var isAccountPath = path.StartsWithSegments("/accounts");
    var isTransactionPath = path.StartsWithSegments("/transactions");

    if (((isAdminPath && !isAdminLoginPath) || isUserPath || isAccountPath || isTransactionPath) && context.User.Identity?.IsAuthenticated != true)
    {
        var returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
        var loginPath = isAdminPath ? "/admin/login" : "/";
        context.Response.Redirect($"{loginPath}?returnUrl={Uri.EscapeDataString(returnUrl)}");
        return;
    }

    if (isAdminPath && !isAdminLoginPath && !context.User.IsInRole("ADMIN"))
    {
        context.Response.Redirect("/access-denied");
        return;
    }

    await next();
});
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();

app.MapPost("/auth/login", async (HttpContext context, IAuthService authService, CancellationToken cancellationToken) =>
{
    var form = await context.Request.ReadFormAsync(cancellationToken);
    var returnUrl = form["ReturnUrl"].ToString();
    var requiredRole = form["RequiredRole"].ToString();
    var loginPath = GetSafeLocalUrl(form["LoginPath"].ToString(), "/");
    var request = new LoginRequestDto
    {
        UsernameOrEmail = form["UsernameOrEmail"].ToString(),
        Password = form["Password"].ToString()
    };

    var result = await authService.LoginAsync(request, cancellationToken);
    if (!result.Success)
    {
        return Results.Redirect(BuildLoginRedirect(loginPath, returnUrl, result.ErrorMessage ?? "Нэвтрэхэд алдаа гарлаа"));
    }

    if (!string.IsNullOrWhiteSpace(requiredRole)
        && !string.Equals(result.Role, requiredRole, StringComparison.OrdinalIgnoreCase))
    {
        return Results.Redirect(BuildLoginRedirect(loginPath, returnUrl, "Энэ хэсэгт нэвтрэх эрхгүй байна"));
    }

    var sessionTimeout = string.Equals(result.Role, "ADMIN", StringComparison.OrdinalIgnoreCase)
        ? AuthConstants.AdminSessionTimeout
        : AuthConstants.UserSessionTimeout;
    var expiresUtc = DateTimeOffset.UtcNow.Add(sessionTimeout);
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, result.UserId!.Value.ToString()),
        new(ClaimTypes.Name, result.Username ?? string.Empty),
        new(ClaimTypes.Role, result.Role ?? "USER"),
        new("FullName", result.FullName ?? string.Empty),
        new(AuthConstants.SessionExpiresUtcTicksClaim, expiresUtc.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture))
    };

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new ClaimsPrincipal(identity);

    await context.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        principal,
        new AuthenticationProperties
        {
            IsPersistent = true,
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = expiresUtc,
            AllowRefresh = false
        });

    var fallbackUrl = string.Equals(result.Role, "ADMIN", StringComparison.OrdinalIgnoreCase)
        ? "/admin/dashboard"
        : "/dashboard";

    return Results.Redirect(GetSafeLocalUrl(returnUrl, fallbackUrl));
});

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
            ExpectedUpdatedAtTicks = expectedUpdatedAtTicks
        },
        cancellationToken);

    var queryName = result.Success ? "success" : "error";
    return Results.Redirect(AppendQuery(returnUrl, queryName, result.ErrorMessage ?? "Review status шинэчлэх үед алдаа гарлаа."));
}).RequireAuthorization(policy => policy.RequireRole("ADMIN"));

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

app.MapPost("/transactions/create/submit", async (HttpContext context, ITransactionService transactionService, CancellationToken cancellationToken) =>
{
    var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!long.TryParse(userIdValue, out var userId))
    {
        return Results.Redirect("/?error=Та%20нэвтрээгүй%20байна.");
    }

    var form = await context.Request.ReadFormAsync(cancellationToken);
    var fromAccountIdValue = form["FromAccountId"].ToString();
    var amountValue = form["Amount"].ToString();
    var transferType = form["TransferType"].ToString();
    var createTransactionPath = string.Equals(transferType, "own", StringComparison.OrdinalIgnoreCase)
        ? "/transactions/create?type=own"
        : "/transactions/create";
    var createTransactionSeparator = createTransactionPath.Contains('?', StringComparison.Ordinal) ? "&" : "?";

    if (!long.TryParse(fromAccountIdValue, out var fromAccountId) || !TryReadDecimal(amountValue, out var amount))
    {
        return Results.Redirect($"{createTransactionPath}{createTransactionSeparator}error={Uri.EscapeDataString("Гүйлгээний хүсэлт буруу байна.")}");
    }

    var dto = new CreateTransactionDto
    {
        FromAccountId = fromAccountId,
        ToAccountNumber = form["ToAccountNumber"].ToString(),
        Amount = amount,
        Description = form["Description"].ToString()
    };

    var result = await transactionService.CreateTransactionAsync(userId, dto, cancellationToken);
    if (!result.Success)
    {
        return Results.Redirect($"{createTransactionPath}{createTransactionSeparator}error={Uri.EscapeDataString(result.ErrorMessage ?? "Гүйлгээ хийх үед алдаа гарлаа.")}");
    }

    return Results.Redirect($"{createTransactionPath}{createTransactionSeparator}success={Uri.EscapeDataString("Гүйлгээ амжилттай хийгдлээ.")}&transactionId={result.TransactionId}");
}).RequireAuthorization();

app.MapPost("/auth/logout", async (HttpContext context, ISecurityEventService securityEventService, CancellationToken cancellationToken) =>
{
    long? userId = null;
    if (long.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId))
    {
        userId = parsedUserId;
    }

    await securityEventService.LogAsync(
        userId,
        context.User.Identity?.Name,
        "LOGOUT",
        true,
        "User logged out.",
        cancellationToken);

    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
}).RequireAuthorization();

app.MapGet("/auth/session-expired", async (HttpContext context, ISecurityEventService securityEventService, CancellationToken cancellationToken) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        long? userId = null;
        if (long.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId))
        {
            userId = parsedUserId;
        }

        await securityEventService.LogAsync(
            userId,
            context.User.Identity?.Name,
            "SESSION_EXPIRED",
            true,
            "User session expired.",
            cancellationToken);
    }

    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect($"/?error={Uri.EscapeDataString("Нэвтрэх хугацаа дууссан тул дахин нэвтэрнэ үү.")}");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static string BuildLoginRedirect(string loginPath, string? returnUrl, string errorMessage)
{
    var query = $"error={Uri.EscapeDataString(errorMessage)}";
    if (!string.IsNullOrWhiteSpace(returnUrl) && IsLocalUrl(returnUrl))
    {
        query += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
    }

    return $"{loginPath}?{query}";
}

static string GetSafeLocalUrl(string? returnUrl, string fallbackUrl)
{
    return !string.IsNullOrWhiteSpace(returnUrl) && IsLocalUrl(returnUrl)
        ? returnUrl
        : fallbackUrl;
}

static string AppendQuery(string url, string key, string value)
{
    var separator = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
    return $"{url}{separator}{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";
}

static bool IsLocalUrl(string url)
{
    return url.StartsWith("/", StringComparison.Ordinal)
        && !url.StartsWith("//", StringComparison.Ordinal)
        && !url.StartsWith("/\\", StringComparison.Ordinal);
}

static bool TryReadBoolean(string value, out bool result)
{
    switch (value.Trim().ToLowerInvariant())
    {
        case "true":
        case "on":
        case "1":
        case "yes":
            result = true;
            return true;
        case "false":
        case "off":
        case "0":
        case "no":
            result = false;
            return true;
        default:
            result = false;
            return false;
    }
}

static bool TryReadCurrentUserId(HttpContext context, out long userId)
{
    var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
    return long.TryParse(userIdValue, out userId);
}

static bool TryReadDecimal(string value, out decimal result)
{
    return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result)
        || decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out result);
}

static decimal? TryReadNullableDecimal(string value)
{
    return string.IsNullOrWhiteSpace(value) ? null : TryReadDecimal(value, out var result) ? result : null;
}

static bool TryReadDateTime(string value, out DateTime result)
{
    return DateTime.TryParseExact(value, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out result)
        || DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out result);
}
