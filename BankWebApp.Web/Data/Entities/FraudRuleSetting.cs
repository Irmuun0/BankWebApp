namespace BankWebApp.Web.Data.Entities;

public partial class FraudRuleSetting
{
    public int Id { get; set; }

    public string RuleCode { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsEnabled { get; set; }

    public int Score { get; set; }

    public decimal? NumericThreshold { get; set; }

    public decimal? AmountThresholdMnt { get; set; }

    public decimal? AmountThresholdUsd { get; set; }

    public int SuspiciousThreshold { get; set; }

    public DateTime UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual User? UpdatedByNavigation { get; set; }
}
