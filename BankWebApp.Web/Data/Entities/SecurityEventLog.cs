using System;

namespace BankWebApp.Web.Data.Entities;

public partial class SecurityEventLog
{
    public long Id { get; set; }

    public long? UserId { get; set; }

    public string? UsernameOrEmail { get; set; }

    public string EventType { get; set; } = null!;

    public bool Success { get; set; }

    public string? Message { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CreatedAtUtc { get; set; }

    public virtual User? User { get; set; }
}
