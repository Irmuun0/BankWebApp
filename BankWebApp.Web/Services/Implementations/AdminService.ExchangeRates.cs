using BankWebApp.Web.Data.Entities;
using BankWebApp.Web.DTOs.Admin;
using BankWebApp.Web.Helpers;
using Microsoft.EntityFrameworkCore;

namespace BankWebApp.Web.Services.Implementations;

// AdminService-ийн ExchangeRates үүрэгтэй хэсэг; тусдаа instance үүсгэхгүй.
public partial class AdminService
{
    public async Task<List<AdminCurrencyRateSettingDto>> GetCurrencyRateSettingsAsync(CancellationToken cancellationToken = default)
    {
        var now = MongoliaClock.Now;
        var settings = await _dbContext.CurrencyRateSettings
            .Include(setting => setting.UpdatedByNavigation)
            .Include(setting => setting.CurrencyRateOverrideSchedules)
                .ThenInclude(schedule => schedule.CreatedByNavigation)
            .Include(setting => setting.CurrencyRateOverrideSchedules)
                .ThenInclude(schedule => schedule.CancelledByNavigation)
            .OrderBy(setting => setting.CurrencyCode)
            .ThenBy(setting => setting.BaseCurrency)
            .ToListAsync(cancellationToken);

        var changed = false;
        foreach (var setting in settings.Where(setting =>
                     setting.IsManualOverride &&
                     setting.ManualExpiresAt is not null &&
                     setting.ManualExpiresAt <= now))
        {
            AddCurrencyRateAudit(
                setting,
                "MANUAL_OVERRIDE_EXPIRED",
                setting.UpdatedBy,
                setting.ManualBuyRate,
                setting.ManualSellRate,
                null,
                null,
                setting.IsManualOverride,
                false,
                setting.ManualExpiresAt,
                null,
                "Manual override expired automatically.");

            setting.IsManualOverride = false;
            setting.ManualBuyRate = null;
            setting.ManualSellRate = null;
            setting.ManualExpiresAt = null;
            setting.UpdatedAt = now;
            changed = true;
        }

        if (changed)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return settings.Select(setting => MapCurrencyRateSetting(setting, now)).ToList();
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateCurrencyRateAlgorithmAsync(
        long adminUserId,
        UpdateCurrencyRateAlgorithmDto dto,
        CancellationToken cancellationToken = default)
    {
        var setting = await _dbContext.CurrencyRateSettings
            .Include(item => item.CurrencyRateOverrideSchedules)
            .FirstOrDefaultAsync(item => item.Id == dto.SettingId, cancellationToken);
        if (setting is null)
        {
            return (false, "Ханшийн тохиргоо олдсонгүй.");
        }

        if (dto.BuyMarginPercent < 0 || dto.BuyMarginPercent > 20 || dto.SellMarginPercent < 0 || dto.SellMarginPercent > 20)
        {
            return (false, "Margin percent 0-20 хооронд байх ёстой.");
        }

        var oldBuyRate = setting.AlgoBuyRate;
        var oldSellRate = setting.AlgoSellRate;
        var oldManual = setting.IsManualOverride;
        var oldExpiresAt = setting.ManualExpiresAt;
        var buyMargin = dto.BuyMarginPercent / 100m;
        var sellMargin = dto.SellMarginPercent / 100m;

        setting.AlgoBuyMarginPercent = buyMargin;
        setting.AlgoSellMarginPercent = sellMargin;
        setting.AlgoBuyRate = CalculateBuyRate(setting.BaseRate, buyMargin);
        setting.AlgoSellRate = CalculateSellRate(setting.BaseRate, sellMargin);
        setting.UpdatedAt = MongoliaClock.Now;
        setting.UpdatedBy = adminUserId;

        AddCurrencyRateAudit(
            setting,
            "ALGORITHM_MARGIN_UPDATED",
            adminUserId,
            oldBuyRate,
            oldSellRate,
            setting.AlgoBuyRate,
            setting.AlgoSellRate,
            oldManual,
            setting.IsManualOverride,
            oldExpiresAt,
            setting.ManualExpiresAt,
            $"Algorithm margins updated. Buy={dto.BuyMarginPercent:N4}%, Sell={dto.SellMarginPercent:N4}%.");

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, "Алгоритмын авах/зарах margin амжилттай шинэчлэгдлээ.");
    }

    public async Task<(bool Success, string? ErrorMessage)> SetManualCurrencyRateOverrideAsync(
        long adminUserId,
        SetManualCurrencyRateOverrideDto dto,
        CancellationToken cancellationToken = default)
    {
        var setting = await _dbContext.CurrencyRateSettings.FirstOrDefaultAsync(item => item.Id == dto.SettingId, cancellationToken);
        if (setting is null)
        {
            return (false, "Ханшийн тохиргоо олдсонгүй.");
        }

        var mode = dto.AdjustmentMode.Trim().ToUpperInvariant();
        if (mode is not ("PERCENT" or "AMOUNT"))
        {
            return (false, "Manual override горим буруу байна.");
        }

        if (dto.BuyAdjustment < 0 || dto.SellAdjustment < 0)
        {
            return (false, "Нэмэгдүүлэх утга сөрөг байж болохгүй.");
        }

        if (mode == "PERCENT" && (dto.BuyAdjustment > 20 || dto.SellAdjustment > 20))
        {
            return (false, "Manual percent нэмэгдэл 0-20 хооронд байх ёстой.");
        }

        if (mode == "AMOUNT" && (dto.BuyAdjustment > 5000 || dto.SellAdjustment > 5000))
        {
            return (false, "Manual MNT нэмэгдэл 0-5000 хооронд байх ёстой.");
        }

        var startsAt = TrimToMinute(dto.StartsAt);
        var endsAt = TrimToMinute(dto.EndsAt);
        var now = TrimToMinute(MongoliaClock.Now);
        if (startsAt < now)
        {
            if (now - startsAt > TimeSpan.FromMinutes(2))
            {
                return (false, "Эхлэх огноо цаг одоогийн цагаас өмнө байж болохгүй.");
            }

            startsAt = now;
        }

        if (endsAt <= startsAt)
        {
            return (false, "Дуусах огноо цаг эхлэх огноо цагаас хойш байх ёстой.");
        }

        if (endsAt - startsAt > TimeSpan.FromDays(30))
        {
            return (false, "Manual override төлөвлөгөө хамгийн ихдээ 30 өдөр үргэлжилнэ.");
        }

        var hasOverlap = await _dbContext.CurrencyRateOverrideSchedules.AnyAsync(schedule =>
            schedule.CurrencyRateSettingId == setting.Id &&
            schedule.Status != "CANCELLED" &&
            startsAt < schedule.EndsAt &&
            endsAt > schedule.StartsAt,
            cancellationToken);
        if (hasOverlap)
        {
            return (false, "Энэ валют дээр тухайн хугацаанд давхцсан manual override төлөвлөгөө байна.");
        }

        var manualBuyRate = mode == "PERCENT"
            ? NormalizeBuyRate(setting.AlgoBuyRate * (1m + dto.BuyAdjustment / 100m))
            : NormalizeBuyRate(setting.AlgoBuyRate + dto.BuyAdjustment);
        var manualSellRate = mode == "PERCENT"
            ? NormalizeSellRate(setting.AlgoSellRate * (1m + dto.SellAdjustment / 100m))
            : NormalizeSellRate(setting.AlgoSellRate + dto.SellAdjustment);

        if (manualBuyRate <= 0 || manualSellRate <= 0)
        {
            return (false, "Manual авах/зарах ханш 0-ээс их байх ёстой.");
        }

        var schedule = new CurrencyRateOverrideSchedule
        {
            CurrencyRateSettingId = setting.Id,
            ManualBuyRate = manualBuyRate,
            ManualSellRate = manualSellRate,
            StartsAt = startsAt,
            EndsAt = endsAt,
            Status = "SCHEDULED",
            CreatedBy = adminUserId,
            CreatedAt = MongoliaClock.Now,
            Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim()
        };

        _dbContext.CurrencyRateOverrideSchedules.Add(schedule);
        setting.UpdatedAt = now;
        setting.UpdatedBy = adminUserId;

        AddCurrencyRateAudit(
            setting,
            "MANUAL_OVERRIDE_SCHEDULED",
            adminUserId,
            setting.AlgoBuyRate,
            setting.AlgoSellRate,
            manualBuyRate,
            manualSellRate,
            false,
            true,
            null,
            endsAt,
            $"Manual override scheduled by {mode}. StartsAt={startsAt:yyyy-MM-dd HH:mm}, EndsAt={endsAt:yyyy-MM-dd HH:mm}, BuyAdjustment={dto.BuyAdjustment}, SellAdjustment={dto.SellAdjustment}.");

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.GetBaseException().Message.Contains("Overlapping currency rate override schedule", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Энэ валют дээр тухайн хугацаанд давхцсан manual override төлөвлөгөө байна.");
        }

        return (true, "Manual авах/зарах ханшийн төлөвлөгөө амжилттай үүслээ.");
    }

    public async Task<(bool Success, string? ErrorMessage)> CancelCurrencyRateOverrideScheduleAsync(
        long adminUserId,
        long scheduleId,
        CancellationToken cancellationToken = default)
    {
        var schedule = await _dbContext.CurrencyRateOverrideSchedules
            .Include(item => item.CurrencyRateSetting)
            .FirstOrDefaultAsync(item => item.Id == scheduleId, cancellationToken);
        if (schedule is null)
        {
            return (false, "Manual override төлөвлөгөө олдсонгүй.");
        }

        if (schedule.Status == "CANCELLED")
        {
            return (true, "Manual override төлөвлөгөө аль хэдийн цуцлагдсан байна.");
        }

        var setting = schedule.CurrencyRateSetting;
        var now = MongoliaClock.Now;
        schedule.Status = "CANCELLED";
        schedule.CancelledBy = adminUserId;
        schedule.CancelledAt = now;
        setting.UpdatedAt = now;
        setting.UpdatedBy = adminUserId;

        AddCurrencyRateAudit(
            setting,
            "MANUAL_OVERRIDE_CANCELLED",
            adminUserId,
            schedule.ManualBuyRate,
            schedule.ManualSellRate,
            null,
            null,
            true,
            false,
            schedule.EndsAt,
            null,
            $"Manual override schedule #{schedule.Id} cancelled by admin.");

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, "Manual override төлөвлөгөө цуцлагдлаа.");
    }

    private static AdminCurrencyRateSettingDto MapCurrencyRateSetting(CurrencyRateSetting setting, DateTime now)
    {
        var activeSchedule = GetActiveSchedule(setting, now);
        var isLegacyManualActive = activeSchedule is null &&
                                   setting.IsManualOverride &&
                                   setting.ManualBuyRate is not null &&
                                   setting.ManualSellRate is not null &&
                                   (setting.ManualExpiresAt is null || setting.ManualExpiresAt > now);
        var isManualActive = activeSchedule is not null || isLegacyManualActive;

        return new AdminCurrencyRateSettingDto
        {
            Id = setting.Id,
            CurrencyCode = setting.CurrencyCode,
            BaseCurrency = setting.BaseCurrency,
            BaseRate = setting.BaseRate,
            AlgoBuyMarginPercent = setting.AlgoBuyMarginPercent * 100m,
            AlgoSellMarginPercent = setting.AlgoSellMarginPercent * 100m,
            AlgoBuyRate = setting.AlgoBuyRate,
            AlgoSellRate = setting.AlgoSellRate,
            IsManualOverride = setting.IsManualOverride,
            IsManualOverrideActive = isManualActive,
            ManualBuyRate = activeSchedule?.ManualBuyRate ?? setting.ManualBuyRate,
            ManualSellRate = activeSchedule?.ManualSellRate ?? setting.ManualSellRate,
            ManualExpiresAt = activeSchedule?.EndsAt ?? setting.ManualExpiresAt,
            ActiveBuyRate = activeSchedule?.ManualBuyRate ?? (isLegacyManualActive ? setting.ManualBuyRate!.Value : setting.AlgoBuyRate),
            ActiveSellRate = activeSchedule?.ManualSellRate ?? (isLegacyManualActive ? setting.ManualSellRate!.Value : setting.AlgoSellRate),
            RateDate = setting.RateDate,
            Source = setting.Source,
            FetchedAt = setting.FetchedAt,
            UpdatedAt = setting.UpdatedAt,
            UpdatedByUsername = setting.UpdatedByNavigation?.Username,
            OverrideSchedules = setting.CurrencyRateOverrideSchedules
                .OrderBy(schedule => schedule.StartsAt)
                .ThenBy(schedule => schedule.Id)
                .Select(schedule => MapCurrencyRateOverrideSchedule(setting, schedule, now))
                .ToList()
        };
    }

    private static AdminCurrencyRateOverrideScheduleDto MapCurrencyRateOverrideSchedule(
        CurrencyRateSetting setting,
        CurrencyRateOverrideSchedule schedule,
        DateTime now)
    {
        return new AdminCurrencyRateOverrideScheduleDto
        {
            Id = schedule.Id,
            SettingId = setting.Id,
            CurrencyCode = setting.CurrencyCode,
            BaseCurrency = setting.BaseCurrency,
            ManualBuyRate = schedule.ManualBuyRate,
            ManualSellRate = schedule.ManualSellRate,
            StartsAt = schedule.StartsAt,
            EndsAt = schedule.EndsAt,
            Status = schedule.Status,
            DisplayStatus = GetScheduleDisplayStatus(schedule, now),
            CreatedByUsername = schedule.CreatedByNavigation?.Username,
            CreatedAt = schedule.CreatedAt,
            CancelledByUsername = schedule.CancelledByNavigation?.Username,
            CancelledAt = schedule.CancelledAt,
            Note = schedule.Note
        };
    }

    private static CurrencyRateOverrideSchedule? GetActiveSchedule(CurrencyRateSetting setting, DateTime now)
    {
        return setting.CurrencyRateOverrideSchedules
            .Where(schedule =>
                schedule.Status != "CANCELLED" &&
                schedule.StartsAt <= now &&
                schedule.EndsAt > now)
            .OrderByDescending(schedule => schedule.StartsAt)
            .FirstOrDefault();
    }

    private static string GetScheduleDisplayStatus(CurrencyRateOverrideSchedule schedule, DateTime now)
    {
        if (schedule.Status == "CANCELLED")
        {
            return "CANCELLED";
        }

        if (schedule.StartsAt > now)
        {
            return "SCHEDULED";
        }

        return schedule.EndsAt <= now ? "EXPIRED" : "ACTIVE";
    }

    private static DateTime TrimToMinute(DateTime value)
    {
        return new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Kind);
    }

    private void AddCurrencyRateAudit(
        CurrencyRateSetting setting,
        string action,
        long? changedBy,
        decimal? oldBuyRate,
        decimal? oldSellRate,
        decimal? newBuyRate,
        decimal? newSellRate,
        bool? oldIsManualOverride,
        bool? newIsManualOverride,
        DateTime? oldManualExpiresAt,
        DateTime? newManualExpiresAt,
        string note)
    {
        _dbContext.CurrencyRateSettingAudits.Add(new CurrencyRateSettingAudit
        {
            CurrencyRateSetting = setting,
            Action = action,
            OldBuyRate = oldBuyRate,
            OldSellRate = oldSellRate,
            NewBuyRate = newBuyRate,
            NewSellRate = newSellRate,
            OldIsManualOverride = oldIsManualOverride,
            NewIsManualOverride = newIsManualOverride,
            OldManualExpiresAt = oldManualExpiresAt,
            NewManualExpiresAt = newManualExpiresAt,
            ChangedBy = changedBy,
            ChangedAt = MongoliaClock.Now,
            Note = note
        });
    }

    private static TimeSpan? BuildManualOverrideDuration(int value, string unit)
    {
        if (value <= 0)
        {
            return null;
        }

        return unit.Trim().ToUpperInvariant() switch
        {
            "MINUTES" when value <= 1440 => TimeSpan.FromMinutes(value),
            "HOURS" when value <= 168 => TimeSpan.FromHours(value),
            "DAYS" when value <= 30 => TimeSpan.FromDays(value),
            _ => null
        };
    }

    private static decimal CalculateBuyRate(decimal baseRate, decimal marginPercent)
    {
        return NormalizeBuyRate(baseRate * (1m - marginPercent));
    }

    private static decimal CalculateSellRate(decimal baseRate, decimal marginPercent)
    {
        return NormalizeSellRate(baseRate * (1m + marginPercent));
    }

    private static decimal NormalizeBuyRate(decimal value)
    {
        return decimal.Floor(value);
    }

    private static decimal NormalizeSellRate(decimal value)
    {
        return decimal.Ceiling(value);
    }
}
