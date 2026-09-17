using BankWebApp.Web.Constants;
using System.Globalization;
using System.Security.Claims;

namespace BankWebApp.Web.Endpoints;

// HTTP form унших болон redirect хийх нийтлэг дүрмүүд.
internal static class EndpointHelpers
{
    internal static string BuildLoginRedirect(string loginPath, string? returnUrl, string errorMessage)
    {
        var redirectUrl = AppendQuery(loginPath, "error", errorMessage);
        if (!string.IsNullOrWhiteSpace(returnUrl) && IsLocalUrl(returnUrl))
        {
            redirectUrl = AppendQuery(redirectUrl, "returnUrl", returnUrl);
        }

        return redirectUrl;
    }

    // Form-оос ирсэн returnUrl-г зөвхөн энэ сайтын доторх хаяг бол ашиглана.
    internal static string GetSafeLocalUrl(string? returnUrl, string fallbackUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl) && IsLocalUrl(returnUrl)
            ? returnUrl
            : fallbackUrl;
    }

    internal static string AppendQuery(string url, string key, string value)
    {
        var separator = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{url}{separator}{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";
    }

    internal static bool IsLocalUrl(string url)
    {
        return url.StartsWith("/", StringComparison.Ordinal)
            && !url.StartsWith("//", StringComparison.Ordinal)
            && !url.StartsWith("/\\", StringComparison.Ordinal);
    }

    internal static bool TryReadBoolean(string value, out bool result)
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

    internal static bool TryReadCurrentUserId(HttpContext context, out long userId)
    {
        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(userIdValue, out userId);
    }

    internal static bool IsPasswordResetAllowedPath(PathString path)
    {
        return path.StartsWithSegments("/profile")
            || path.StartsWithSegments("/auth/logout")
            || path.StartsWithSegments("/_framework")
            || path.StartsWithSegments("/_content")
            || path.StartsWithSegments("/css")
            || path.StartsWithSegments("/js")
            || path.StartsWithSegments("/images")
            || path.StartsWithSegments("/lib");
    }

    // Profile шинэчлэхэд байгаа session-ийн дуусах хугацааг хадгална.
    internal static DateTimeOffset GetCurrentSessionExpiry(HttpContext context, string role)
    {
        var claimValue = context.User.FindFirstValue(AuthConstants.SessionExpiresUtcTicksClaim);
        if (long.TryParse(claimValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
        {
            var currentExpiry = new DateTimeOffset(new DateTime(ticks, DateTimeKind.Utc));
            if (currentExpiry > DateTimeOffset.UtcNow)
            {
                return currentExpiry;
            }
        }

        var timeout = string.Equals(role, "ADMIN", StringComparison.OrdinalIgnoreCase)
            ? AuthConstants.AdminSessionTimeout
            : AuthConstants.UserSessionTimeout;
        return DateTimeOffset.UtcNow.Add(timeout);
    }

    internal static bool TryReadDecimal(string value, out decimal result)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result)
            || decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out result);
    }

    internal static decimal? TryReadNullableDecimal(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : TryReadDecimal(value, out var result) ? result : null;
    }

    internal static bool TryReadDateTime(string value, out DateTime result)
    {
        return DateTime.TryParseExact(value, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out result)
            || DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out result);
    }
}
