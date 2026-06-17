using System;

namespace BankWebApp.Web.Data.Entities;

public partial class TransactionDetectionLog
{
    public long Id { get; set; }

    public long TransactionId { get; set; }

    public string ServiceStatus { get; set; } = null!;

    public bool? IsSuspicious { get; set; }

    public decimal? RiskScore { get; set; }

    public string? Reason { get; set; }

    public string? TriggeredRules { get; set; }

    public string Source { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public virtual Transaction Transaction { get; set; } = null!;
}
