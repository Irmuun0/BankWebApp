using System;
using System.Collections.Generic;

namespace BankWebApp.Web.Data.Entities;

public partial class KnowledgeBase
{
    public long Id { get; set; }

    public string Category { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    public string? Keywords { get; set; }

    public string Version { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
