namespace BankWebApp.Web.Helpers;

public static class Money
{
    // DB дизайны дүрэм: тоймлохгүй, 2 бутархай орноор тасална (0.995 → 0.99).
    // UI preview болон backend ижил тооцоолол ашиглахын тулд нэг газар хадгалав.
    public static decimal TruncateMoney(decimal value)
    {
        return decimal.Truncate(value * 100m) / 100m;
    }
}
