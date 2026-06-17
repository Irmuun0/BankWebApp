namespace BankWebApp.Web.Constants;

public static class AuthConstants
{
    public static readonly TimeSpan UserSessionTimeout = TimeSpan.FromMinutes(30);

    public static readonly TimeSpan AdminSessionTimeout = TimeSpan.FromHours(8);

    public const string SessionExpiresUtcTicksClaim = "SessionExpiresUtcTicks";
}
