namespace BankWebApp.Web.DTOs.Admin;

public class AdminFraudRuleSettingsPageDto
{
    public int SuspiciousThreshold { get; set; } = 60;

    public DateTime? ThresholdUpdatedAt { get; set; }

    public string? ThresholdUpdatedByUsername { get; set; }

    public List<AdminFraudRuleSettingDto> Rules { get; set; } = [];
}
