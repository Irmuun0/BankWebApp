namespace BankWebApp.Web.Helpers;

public static class MongoliaClock
{
    private static readonly TimeZoneInfo? UlaanbaatarTimeZone = ResolveUlaanbaatarTimeZone();

    public static DateTime Now => ToMongoliaTime(DateTime.UtcNow);

    public static DateOnly Today => DateOnly.FromDateTime(Now);

    public static DateTime ToMongoliaTime(DateTime utcDateTime)
    {
        var normalizedUtc = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);

        return UlaanbaatarTimeZone is null
            ? normalizedUtc.AddHours(8)
            : TimeZoneInfo.ConvertTimeFromUtc(normalizedUtc, UlaanbaatarTimeZone);
    }

    private static TimeZoneInfo? ResolveUlaanbaatarTimeZone()
    {
        foreach (var timeZoneId in new[] { "Asia/Ulaanbaatar", "Ulaanbaatar Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return null;
    }
}
