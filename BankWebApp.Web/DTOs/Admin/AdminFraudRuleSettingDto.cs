namespace BankWebApp.Web.DTOs.Admin;

public class AdminFraudRuleSettingDto
{
    public int Id { get; set; }

    public string RuleCode { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsEnabled { get; set; }

    public int Score { get; set; }

    public decimal? NumericThreshold { get; set; }

    public decimal? AmountThresholdMnt { get; set; }

    public decimal? AmountThresholdUsd { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? UpdatedByUsername { get; set; }
}
