using System;
using System.Collections.Generic;

namespace BankWebApp.Web.Data.Entities;

public partial class Account
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public string AccountNumber { get; set; } = null!;

    public string AccountType { get; set; } = null!;

    public string Currency { get; set; } = null!;

    public decimal Balance { get; set; }

    public bool IsActive { get; set; }

    public bool IsPrimary { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Transaction> TransactionFromAccounts { get; set; } = new List<Transaction>();

    public virtual ICollection<Transaction> TransactionToAccounts { get; set; } = new List<Transaction>();

    public virtual User User { get; set; } = null!;
}
