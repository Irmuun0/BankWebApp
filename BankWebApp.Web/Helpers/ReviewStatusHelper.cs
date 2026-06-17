namespace BankWebApp.Web.Helpers;

public static class ReviewStatusHelper
{
    public static readonly string[] AllowedStatuses =
    [
        "PENDING",
        "REVIEWING",
        "CONFIRMED",
        "FALSE_ALARM",
        "RESOLVED"
    ];

    public static bool IsValid(string? status)
    {
        return !string.IsNullOrWhiteSpace(status)
            && AllowedStatuses.Contains(status.Trim().ToUpperInvariant());
    }

    public static string Normalize(string status)
    {
        return status.Trim().ToUpperInvariant();
    }

    public static string GetLabel(string? status)
    {
        return status?.Trim().ToUpperInvariant() switch
        {
            "PENDING" => "Шалгах хүлээгдэж буй",
            "REVIEWING" => "Шалгаж буй",
            "CONFIRMED" => "Сэжигтэй гэж баталгаажсан",
            "FALSE_ALARM" => "Сэжигтэй биш гэж тогтоогдсон",
            "RESOLVED" => "Шийдвэрлэсэн",
            _ => status ?? string.Empty
        };
    }
}
