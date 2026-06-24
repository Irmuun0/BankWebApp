namespace BankWebApp.Web.DTOs.Ai;

public class SuspiciousDetectionRequestDto
{
    public long TransactionId { get; set; }
    public long SenderUserId { get; set; }
    public decimal Amount { get; set; }
    public string SourceCurrency { get; set; } = string.Empty;
    public decimal CreditedAmount { get; set; }
    public string TargetCurrency { get; set; } = string.Empty;
    public bool IsCrossCurrency { get; set; }
    public string? Description { get; set; }
    public int CreatedHour { get; set; }
    public decimal SenderAverageAmountLast30Days { get; set; }
    public decimal SenderMaxAmountLast30Days { get; set; }
    public int SenderTransactionCountLast24Hours { get; set; }
    public int SmallTransactionCountLast24Hours { get; set; }
    public decimal SmallTransactionTotalLast24Hours { get; set; }
    public int DistinctReceiverCountLast24Hours { get; set; }
    public int DistinctSenderCountToReceiverLast24Hours { get; set; }
    public decimal RecentInboundAmountLast30Minutes { get; set; }
    public int SenderAccountAgeDays { get; set; }
    public int? SenderDaysSinceLastTransaction { get; set; }
    public SuspiciousDetectionSettingsDto? DetectionSettings { get; set; }
}

public class SuspiciousDetectionSettingsDto
{
    public int SuspiciousThreshold { get; set; } = 60;

    public List<SuspiciousDetectionRuleSettingDto> Rules { get; set; } = [];
}

public class SuspiciousDetectionRuleSettingDto
{
    public string RuleCode { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public int Score { get; set; }

    public decimal? NumericThreshold { get; set; }

    public decimal? AmountThresholdMnt { get; set; }

    public decimal? AmountThresholdUsd { get; set; }
}
