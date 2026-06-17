namespace BankWebApp.Web.DTOs.Admin;

public class AdminAuditLogDto
{
    public string Source { get; set; } = string.Empty;

    public string SourceLabel { get; set; } = string.Empty;

    public long SourceId { get; set; }

    public DateTime OccurredAt { get; set; }

    public long? ActorUserId { get; set; }

    public string ActorDisplayName { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string ActionLabel { get; set; } = string.Empty;

    public string? TargetType { get; set; }

    public long? TargetId { get; set; }

    public string Summary { get; set; } = string.Empty;

    public string? Detail { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string Severity { get; set; } = "INFO";

    public bool? Success { get; set; }
}
