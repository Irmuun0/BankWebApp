using System;
using System.Collections.Generic;

namespace BankWebApp.Web.Data.Entities;

public partial class Notification
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public long? TransactionId { get; set; }

    public string NotificationType { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Message { get; set; } = null!;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ReadAt { get; set; }

    public virtual Transaction? Transaction { get; set; }

    public virtual User User { get; set; } = null!;
}
