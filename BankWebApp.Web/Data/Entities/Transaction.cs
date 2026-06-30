using System;
using System.Collections.Generic;

namespace BankWebApp.Web.Data.Entities;

public partial class Transaction
{
    public long Id { get; set; }

    public long FromAccountId { get; set; }

    public long ToAccountId { get; set; }

    public decimal Amount { get; set; }

    public string SourceCurrency { get; set; } = null!;

    public decimal CreditedAmount { get; set; }

    public string TargetCurrency { get; set; } = null!;

    public long? ExchangeRateLogId { get; set; }

    public decimal? ExchangeRateValue { get; set; }

    public decimal RoundingDifference { get; set; }

    public string? Description { get; set; }

    public string Status { get; set; } = null!;

    public string? FailureReason { get; set; }

    public bool IsSuspicious { get; set; }

    public DateTime? DetectionCheckedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<ChatLog> ChatLogs { get; set; } = new List<ChatLog>();

    public virtual ICollection<AiTransactionAnalysisLog> AiTransactionAnalysisLogs { get; set; } = new List<AiTransactionAnalysisLog>();

    public virtual ExchangeRateLog? ExchangeRateLog { get; set; }

    public virtual FxIncomeLog? FxIncomeLog { get; set; }

    public virtual Account FromAccount { get; set; } = null!;

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual SuspiciousTransactionDetail? SuspiciousTransactionDetail { get; set; }

    public virtual ICollection<TransactionDetectionLog> TransactionDetectionLogs { get; set; } = new List<TransactionDetectionLog>();

    public virtual Account ToAccount { get; set; } = null!;
}
