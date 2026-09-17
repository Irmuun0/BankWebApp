using BankWebApp.Web.DTOs.Admin;
using BankWebApp.Web.Helpers;
using Microsoft.AspNetCore.Components;
using System.Globalization;

namespace BankWebApp.Web.Components.Pages;

// AdminExchangeRates.razor нь дэлгэц; энэ partial class нь төлөв, event, өгөгдөл ачаалах логик.
public partial class AdminExchangeRates
{
    private static readonly string[] DayNames = ["Ня", "Да", "Мя", "Лх", "Пү", "Ба", "Бя"];
    private List<AdminCurrencyRateSettingDto> settings = [];
    private bool isLoading = true;
    private DateTime calendarMonth = new(MongoliaClock.Today.Year, MongoliaClock.Today.Month, 1);
    private AdminCurrencyRateOverrideScheduleDto? selectedSchedule;

    [SupplyParameterFromQuery]
    public string? Success { get; set; }

    [SupplyParameterFromQuery]
    public string? Error { get; set; }

    private DateTime DefaultStartAt => TrimToMinute(MongoliaClock.Now);

    private DateTime DefaultEndAt => DefaultStartAt.AddHours(2);

    private IEnumerable<DateTime> CalendarDays
    {
        get
        {
            var firstDay = new DateTime(calendarMonth.Year, calendarMonth.Month, 1);
            var start = firstDay.AddDays(-(int)firstDay.DayOfWeek);
            return Enumerable.Range(0, 42).Select(offset => start.AddDays(offset));
        }
    }

    protected override async Task OnInitializedAsync()
    {
        settings = await AdminService.GetCurrencyRateSettingsAsync();
        isLoading = false;
    }

    private IEnumerable<AdminCurrencyRateOverrideScheduleDto> GetSchedulesForDay(DateTime day)
    {
        var dayStart = day.Date;
        var dayEnd = dayStart.AddDays(1);
        return settings
            .SelectMany(setting => setting.OverrideSchedules)
            .Where(schedule =>
                schedule.DisplayStatus != "CANCELLED" &&
                schedule.StartsAt < dayEnd &&
                schedule.EndsAt > dayStart)
            .OrderBy(schedule => schedule.StartsAt);
    }

    private void OpenSchedule(AdminCurrencyRateOverrideScheduleDto schedule)
    {
        selectedSchedule = schedule;
    }

    private void CloseSchedule()
    {
        selectedSchedule = null;
    }

    private void PreviousMonth()
    {
        calendarMonth = calendarMonth.AddMonths(-1);
    }

    private void NextMonth()
    {
        calendarMonth = calendarMonth.AddMonths(1);
    }

    private static DateTime TrimToMinute(DateTime value)
    {
        return new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0);
    }

    private static string FormatDateTimeLocal(DateTime value)
    {
        return value.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
    }

    private static string TranslateStatus(string status)
    {
        return status switch
        {
            "ACTIVE" => "Идэвхтэй",
            "SCHEDULED" => "Төлөвлөгдсөн",
            "EXPIRED" => "Дууссан",
            "CANCELLED" => "Цуцлагдсан",
            _ => status
        };
    }

    private static string DisplayUser(string? username)
    {
        return string.IsNullOrWhiteSpace(username) ? "system" : username;
    }

    private static string GetScheduleColorClass(AdminCurrencyRateOverrideScheduleDto schedule)
    {
        var key = $"{schedule.CurrencyCode}:{schedule.ManualBuyRate:0}:{schedule.ManualSellRate:0}";
        var index = (int)((uint)key.GetHashCode() % 4);
        return $"rate-color-{index}";
    }

    private static string FormatCustomerRate(decimal rate)
    {
        return rate.ToString("N0", CultureInfo.CurrentCulture);
    }
}
