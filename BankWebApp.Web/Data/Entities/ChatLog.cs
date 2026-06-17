using System;
using System.Collections.Generic;

namespace BankWebApp.Web.Data.Entities;

public partial class ChatLog
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public Guid SessionId { get; set; }

    public string IntentType { get; set; } = null!;

    public string UserMessage { get; set; } = null!;

    public string BotResponse { get; set; } = null!;

    public string? UsedContextType { get; set; }

    public string? UsedKnowledgeBaseIds { get; set; }

    public long? RelatedTransactionId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Transaction? RelatedTransaction { get; set; }

    public virtual User User { get; set; } = null!;
}
