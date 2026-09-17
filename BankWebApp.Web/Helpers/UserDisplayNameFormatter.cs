namespace BankWebApp.Web.Helpers;

public static class UserDisplayNameFormatter
{
    public static string Format(string? firstName, string? lastName, string? fallback = null)
    {
        var first = firstName?.Trim();
        var last = lastName?.Trim();

        if (!string.IsNullOrWhiteSpace(last) && !string.IsNullOrWhiteSpace(first))
        {
            return $"{last}-ийн {first}";
        }

        if (!string.IsNullOrWhiteSpace(first))
        {
            return first;
        }

        if (!string.IsNullOrWhiteSpace(last))
        {
            return $"{last}-ийн";
        }

        return fallback?.Trim() ?? string.Empty;
    }

    public static string? FormatOrNull(string? firstName, string? lastName)
    {
        var name = Format(firstName, lastName);
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }
}
