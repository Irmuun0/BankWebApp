namespace BankWebApp.Web.DTOs.Admin;

public class UpdateFraudRuleSettingDto
{
    public int Id { get; set; }

    public bool IsEnabled { get; set; }

    public int Score { get; set; }

    public decimal? NumericThreshold { get; set; }

    public decimal? AmountThresholdMnt { get; set; }

    public decimal? AmountThresholdUsd { get; set; }
}
