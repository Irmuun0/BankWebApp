namespace BankWebApp.Web.DTOs.Ai;

public class UserFinanceChatContextDto
{
    public string GeneratedAt { get; set; } = string.Empty;
    public string PeriodStart { get; set; } = string.Empty;
    public string PeriodEnd { get; set; } = string.Empty;
    public List<UserFinanceAccountContextDto> Accounts { get; set; } = [];
    public List<UserFinanceCurrencySummaryDto> Last30DaysByCurrency { get; set; } = [];
    public List<UserFinanceCurrencySummaryDto> Last15DaysByCurrency { get; set; } = [];
    public List<UserFinanceTransactionContextDto> RecentTransactions { get; set; } = [];
}

public class UserFinanceAccountContextDto
{
    public string AccountMasked { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public decimal DailyLimitMnt { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; }
}

public class UserFinanceCurrencySummaryDto
{
    public string Currency { get; set; } = string.Empty;
    public decimal Income { get; set; }
    public decimal Expense { get; set; }
    public decimal Net { get; set; }
    public int IncomeCount { get; set; }
    public int ExpenseCount { get; set; }
}

public class UserFinanceTransactionContextDto
{
    public long TransactionId { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string FromAccountMasked { get; set; } = string.Empty;
    public string ToAccountMasked { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string SourceCurrency { get; set; } = string.Empty;
    public decimal CreditedAmount { get; set; }
    public string TargetCurrency { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
}
