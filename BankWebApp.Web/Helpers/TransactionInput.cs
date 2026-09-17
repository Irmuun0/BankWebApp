using System.Globalization;

namespace BankWebApp.Web.Helpers;

// Form-ын текст шалгах дүрэм. Үлдэгдэл, эзэмшигч, лимитийг TransactionService дахин шалгана.
public static class TransactionInput
{
    public static bool TryReadTransactionAmount(string value, out decimal result)
    {
        result = 0m;
        if (string.IsNullOrEmpty(value) || value != value.Trim())
        {
            return false;
        }

        var parts = value.Split('.');
        if (parts.Length is < 1 or > 2 ||
            parts[0].Length is < 1 or > 16 ||
            parts[0].Any(character => character is < '0' or > '9'))
        {
            return false;
        }

        if (parts.Length == 2 &&
            (parts[1].Length is < 1 or > 2 || parts[1].Any(character => character is < '0' or > '9')))
        {
            return false;
        }

        return decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out result);
    }

    public static bool IsValidAccountNumber(string value)
    {
        return value.Length == 10 &&
               value.All(character => character is >= '0' and <= '9');
    }
}
