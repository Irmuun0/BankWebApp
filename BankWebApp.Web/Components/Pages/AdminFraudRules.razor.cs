using BankWebApp.Web.DTOs.Admin;
using Microsoft.AspNetCore.Components;

namespace BankWebApp.Web.Components.Pages;

// AdminFraudRules.razor нь дэлгэц; энэ partial class нь төлөв, event, өгөгдөл ачаалах логик.
public partial class AdminFraudRules
{
    private AdminFraudRuleSettingsPageDto pageModel = new();
    private bool isLoading = true;

    [SupplyParameterFromQuery]
    public string? Success { get; set; }

    [SupplyParameterFromQuery]
    public string? Error { get; set; }

    protected override async Task OnInitializedAsync()
    {
        pageModel = await AdminService.GetFraudRuleSettingsAsync();
        isLoading = false;
    }

    private static string? FormatNullable(decimal? value)
    {
        return value?.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool UsesNumericThreshold(string ruleCode)
    {
        return ruleCode is
            "HIGH_AMOUNT_COMPARED_TO_AVERAGE" or
            "NIGHT_TIME_TRANSACTION" or
            "MANY_TRANSACTIONS_LAST_24_HOURS" or
            "MANY_SMALL_TRANSACTIONS" or
            "RAPID_IN_OUT_FLOW" or
            "NEW_ACCOUNT_HIGH_AMOUNT" or
            "DORMANT_ACCOUNT_ACTIVITY" or
            "MANY_RECEIVERS_SHORT_TIME" or
            "MANY_SENDERS_TO_ONE_ACCOUNT";
    }

    private static bool UsesAmountThreshold(string ruleCode)
    {
        return ruleCode is
            "VERY_HIGH_AMOUNT" or
            "HIGH_CROSS_CURRENCY_TRANSACTION" or
            "STRUCTURING_SMALL_SPLIT_TRANSFERS" or
            "NEW_ACCOUNT_HIGH_AMOUNT" or
            "DORMANT_ACCOUNT_ACTIVITY" or
            "DESCRIPTION_AMOUNT_MISMATCH";
    }

    private static string GetNumericThresholdHelp(string ruleCode)
    {
        return ruleCode switch
        {
            "HIGH_AMOUNT_COMPARED_TO_AVERAGE" => "Дундаж гүйлгээний дүнгээс хэд дахин их байвал rule зөрчих вэ. Жишээ: 3 гэвэл 3 дахин их.",
            "NIGHT_TIME_TRANSACTION" => "Шөнийн цагийн дээд хязгаар. Жишээ: 6 гэвэл 00:00-05:59 хооронд хийсэн гүйлгээ.",
            "MANY_TRANSACTIONS_LAST_24_HOURS" => "Сүүлийн 24 цагт хийсэн гүйлгээний тоо энэ утгад хүрвэл rule зөрчинө.",
            "MANY_SMALL_TRANSACTIONS" => "Сүүлийн 24 цагт хийсэн жижиг гүйлгээний тоо энэ утгад хүрвэл rule зөрчинө.",
            "RAPID_IN_OUT_FLOW" => "Сүүлийн 30 минутын орлогоос хэдэн хувийг гаргавал rule зөрчих вэ. Жишээ: 0.8 гэвэл 80%.",
            "NEW_ACCOUNT_HIGH_AMOUNT" => "Данс хэдэн хоногоос шинэ гэж тооцогдох вэ. Жишээ: 7 гэвэл 7 хоног хүртэл.",
            "DORMANT_ACCOUNT_ACTIVITY" => "Хэдэн хоног гүйлгээгүй бол идэвхгүй данс гэж үзэх вэ.",
            "MANY_RECEIVERS_SHORT_TIME" => "24 цагт хэдэн өөр хүлээн авагч руу шилжүүлбэл rule зөрчих вэ.",
            "MANY_SENDERS_TO_ONE_ACCOUNT" => "24 цагт хэдэн өөр илгээгч нэг данс руу мөнгө илгээвэл rule зөрчих вэ.",
            _ => "Энэ rule-ийн тоон босго."
        };
    }
}
