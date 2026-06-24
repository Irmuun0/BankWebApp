using System;

namespace BankWebApp.Web.Data.Entities;

public partial class AiTransactionAnalysisLog
{
    public long Id { get; set; }

    public long TransactionId { get; set; }

    public long AnalyzedBy { get; set; }

    public string ModelName { get; set; } = null!;

    public bool? IsSuspicious { get; set; }

    public decimal? RiskScore { get; set; }

    public string Explanation { get; set; } = null!;

    public string? RecommendedAction { get; set; }

    public string? SourceContextJson { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User AnalyzedByNavigation { get; set; } = null!;

    public virtual Transaction Transaction { get; set; } = null!;
}
