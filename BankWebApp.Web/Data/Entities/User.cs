using System;
using System.Collections.Generic;

namespace BankWebApp.Web.Data.Entities;

public partial class User
{
    public long Id { get; set; }

    public string Username { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string PhoneNumber { get; set; } = null!;

    public string NationalId { get; set; } = null!;

    public string? EmergencyPhoneNumber { get; set; }

    public string Role { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public int FailedLoginCount { get; set; }

    public DateTime? LockedUntil { get; set; }

    public DateTime? LockedUntilUtc { get; set; }

    public DateTime? LastFailedLoginAt { get; set; }

    public DateTime? LastFailedLoginAtUtc { get; set; }

    public DateTime? LastLoginAtUtc { get; set; }

    public long? LockedUntilServerTick { get; set; }

    public long? LastFailedLoginServerTick { get; set; }

    public DateTime? PasswordChangedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Account> Accounts { get; set; } = new List<Account>();

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual ICollection<AiTransactionAnalysisLog> AiTransactionAnalysisLogs { get; set; } = new List<AiTransactionAnalysisLog>();

    public virtual ICollection<ChatLog> ChatLogs { get; set; } = new List<ChatLog>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<SecurityEventLog> SecurityEventLogs { get; set; } = new List<SecurityEventLog>();

    public virtual ICollection<SuspiciousTransactionDetail> SuspiciousTransactionDetails { get; set; } = new List<SuspiciousTransactionDetail>();
}
