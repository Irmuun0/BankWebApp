using System;
using System.Collections.Generic;

namespace BankWebApp.Web.Data.Entities;

public partial class SuspiciousTransactionDetail
{
    public long Id { get; set; }

    public long TransactionId { get; set; }

    public decimal RiskScore { get; set; }

    public string SuspiciousReason { get; set; } = null!;

    public string? AiExplanation { get; set; }

    public string ReviewStatus { get; set; } = null!;

    public string? ReviewNote { get; set; }

    public long? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual User? ReviewedByNavigation { get; set; }

    public virtual Transaction Transaction { get; set; } = null!;
}
