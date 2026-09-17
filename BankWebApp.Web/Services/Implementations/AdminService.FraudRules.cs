using BankWebApp.Web.Data.Entities;
using BankWebApp.Web.DTOs.Admin;
using BankWebApp.Web.Helpers;
using Microsoft.EntityFrameworkCore;

namespace BankWebApp.Web.Services.Implementations;

// AdminService-ийн FraudRules үүрэгтэй хэсэг; тусдаа instance үүсгэхгүй.
public partial class AdminService
{
    public async Task<AdminFraudRuleSettingsPageDto> GetFraudRuleSettingsAsync(CancellationToken cancellationToken = default)
    {
        var detectionSetting = await _dbContext.FraudDetectionSettings
            .AsNoTracking()
            .Include(setting => setting.UpdatedByNavigation)
            .FirstOrDefaultAsync(setting => setting.Id == 1, cancellationToken);

        var rules = await _dbContext.FraudRuleSettings
            .AsNoTracking()
            .Include(setting => setting.UpdatedByNavigation)
            .OrderBy(setting => setting.RuleCode)
            .Select(setting => new AdminFraudRuleSettingDto
            {
                Id = setting.Id,
                RuleCode = setting.RuleCode,
                DisplayName = setting.DisplayName,
                Description = setting.Description,
                IsEnabled = setting.IsEnabled,
                Score = setting.Score,
                NumericThreshold = setting.NumericThreshold,
                AmountThresholdMnt = setting.AmountThresholdMnt,
                AmountThresholdUsd = setting.AmountThresholdUsd,
                UpdatedAt = setting.UpdatedAt,
                UpdatedByUsername = setting.UpdatedByNavigation == null ? null : setting.UpdatedByNavigation.Username
            })
            .ToListAsync(cancellationToken);

        return new AdminFraudRuleSettingsPageDto
        {
            SuspiciousThreshold = detectionSetting?.SuspiciousThreshold ?? 60,
            ThresholdUpdatedAt = detectionSetting?.UpdatedAt,
            ThresholdUpdatedByUsername = detectionSetting?.UpdatedByNavigation?.Username,
            Rules = rules
        };
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateFraudRuleSettingAsync(
        long adminUserId,
        UpdateFraudRuleSettingDto dto,
        CancellationToken cancellationToken = default)
    {
        var setting = await _dbContext.FraudRuleSettings
            .FirstOrDefaultAsync(setting => setting.Id == dto.Id, cancellationToken);

        if (setting is null)
        {
            return (false, "Rule тохиргоо олдсонгүй.");
        }

        if (dto.Score is < 0 or > 100)
        {
            return (false, "Rule score 0-100 хооронд байх ёстой.");
        }

        if (dto.NumericThreshold is < 0 ||
            dto.AmountThresholdMnt is < 0 ||
            dto.AmountThresholdUsd is < 0)
        {
            return (false, "Threshold утга сөрөг байж болохгүй.");
        }

        var oldValue = new
        {
            setting.IsEnabled,
            setting.Score,
            setting.NumericThreshold,
            setting.AmountThresholdMnt,
            setting.AmountThresholdUsd
        };

        setting.IsEnabled = dto.IsEnabled;
        setting.Score = dto.Score;
        setting.NumericThreshold = dto.NumericThreshold;
        setting.AmountThresholdMnt = dto.AmountThresholdMnt;
        setting.AmountThresholdUsd = dto.AmountThresholdUsd;
        setting.UpdatedBy = adminUserId;
        setting.UpdatedAt = MongoliaClock.Now;

        AddAuditLog(
            adminUserId,
            "FRAUD_RULE_SETTING_UPDATED",
            "fraud_rule_settings",
            setting.Id,
            oldValue,
            new
            {
                setting.IsEnabled,
                setting.Score,
                setting.NumericThreshold,
                setting.AmountThresholdMnt,
                setting.AmountThresholdUsd
            },
            $"{setting.RuleCode} rule тохиргоо шинэчлэгдлээ.");

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, "Rule тохиргоо шинэчлэгдлээ.");
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateFraudDetectionSettingsAsync(
        long adminUserId,
        UpdateFraudDetectionSettingsDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto.SuspiciousThreshold is < 1 or > 100)
        {
            return (false, "Сэжигтэй гэж үзэх босго 1-100 хооронд байх ёстой.");
        }

        var setting = await _dbContext.FraudDetectionSettings
            .FirstOrDefaultAsync(setting => setting.Id == 1, cancellationToken);

        if (setting is null)
        {
            setting = new FraudDetectionSetting
            {
                Id = 1,
                SuspiciousThreshold = 60,
                UpdatedAt = MongoliaClock.Now
            };
            _dbContext.FraudDetectionSettings.Add(setting);
        }

        var oldValue = new { setting.SuspiciousThreshold };
        setting.SuspiciousThreshold = dto.SuspiciousThreshold;
        setting.UpdatedBy = adminUserId;
        setting.UpdatedAt = MongoliaClock.Now;

        AddAuditLog(
            adminUserId,
            "FRAUD_DETECTION_SETTING_UPDATED",
            "fraud_detection_settings",
            setting.Id,
            oldValue,
            new { setting.SuspiciousThreshold },
            "Rule-based detection global threshold шинэчлэгдлээ.");

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, "Сэжигтэй босго шинэчлэгдлээ.");
    }

    private async Task<decimal> GetFraudDetectionThresholdAsync(CancellationToken cancellationToken)
    {
        var threshold = await _dbContext.FraudDetectionSettings
            .AsNoTracking()
            .Where(setting => setting.Id == 1)
            .Select(setting => (decimal?)setting.SuspiciousThreshold)
            .FirstOrDefaultAsync(cancellationToken);

        return threshold is > 0 ? threshold.Value : 60m;
    }
}
