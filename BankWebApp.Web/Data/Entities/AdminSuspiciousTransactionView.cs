using System;
using System.Collections.Generic;

namespace BankWebApp.Web.Data.Entities;

public partial class AdminSuspiciousTransactionView
{
    public long TransactionId { get; set; }

    public long FromAccountId { get; set; }

    public long ToAccountId { get; set; }

    public decimal Amount { get; set; }

    public string SourceCurrency { get; set; } = null!;

    public decimal CreditedAmount { get; set; }

    public string TargetCurrency { get; set; } = null!;

    public decimal? ExchangeRateValue { get; set; }

    public string? Description { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public decimal RiskScore { get; set; }

    public string SuspiciousReason { get; set; } = null!;

    public string? AiExplanation { get; set; }

    public string ReviewStatus { get; set; } = null!;

    public string? ReviewNote { get; set; }

    public long? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }
}
