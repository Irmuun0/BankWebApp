using BankWebApp.Web.Components.Charts;
using BankWebApp.Web.Helpers;
using Microsoft.AspNetCore.Components;

namespace BankWebApp.Web.Components.Pages;

// Transactions.razor нь дэлгэц; энэ partial class нь төлөв, event, өгөгдөл ачаалах логик.
public partial class Transactions
{
    [SupplyParameterFromQuery(Name = "accountId")]
    public long? AccountId { get; set; }

    [SupplyParameterFromQuery(Name = "startDate")]
    public DateOnly? StartDate { get; set; }

    [SupplyParameterFromQuery(Name = "endDate")]
    public DateOnly? EndDate { get; set; }

    [SupplyParameterFromQuery(Name = "range")]
    public string? Range { get; set; }

    [SupplyParameterFromQuery(Name = "detail")]
    public long? DetailTransactionId { get; set; }

    private List<BankWebApp.Web.DTOs.Accounts.AccountDto> accounts = [];
    private List<BankWebApp.Web.DTOs.Transactions.UserTransactionDto> transactions = [];
    private BankWebApp.Web.DTOs.Transactions.TransactionReceiptDto? selectedReceipt;
    private bool isLoading = true;
    private string? errorMessage;
    private long selectedAccountId;
    private DateOnly filterStartDate;
    private DateOnly filterEndDate;

    protected override async Task OnParametersSetAsync()
    {
        var userId = CurrentUser.UserId;
        if (userId is null)
        {
            errorMessage = "Та нэвтрээгүй байна.";
            isLoading = false;
            return;
        }

        accounts = await AccountService.GetMyAccountsAsync(userId.Value);
        var selectedAccount = ResolveSelectedAccount();
        if (selectedAccount is null)
        {
            errorMessage = "Гүйлгээ харах данс олдсонгүй.";
            isLoading = false;
            return;
        }

        selectedAccountId = selectedAccount.Id;
        ResolveDateRange();
        transactions = await TransactionService.GetMyTransactionsAsync(
            userId.Value,
            selectedAccountId,
            filterStartDate,
            filterEndDate);

        selectedReceipt = null;
        if (DetailTransactionId is not null)
        {
            selectedReceipt = await TransactionService.GetMyTransactionReceiptAsync(userId.Value, DetailTransactionId.Value);
        }

        isLoading = false;
    }

    private BankWebApp.Web.DTOs.Accounts.AccountDto? ResolveSelectedAccount()
    {
        if (AccountId is not null)
        {
            var requested = accounts.FirstOrDefault(account => account.Id == AccountId.Value);
            if (requested is not null)
            {
                return requested;
            }
        }

        return accounts.FirstOrDefault(account => account.IsPrimary)
            ?? accounts.FirstOrDefault(account => account.IsActive)
            ?? accounts.FirstOrDefault();
    }

    private void ResolveDateRange()
    {
        var today = MongoliaClock.Today;
        (filterStartDate, filterEndDate) = Range switch
        {
            "today" => (today, today),
            "last7" => (today.AddDays(-6), today),
            "last30" => (today.AddDays(-29), today),
            _ => (today.AddDays(-1), today)
        };

        filterStartDate = StartDate ?? filterStartDate;
        filterEndDate = EndDate ?? filterEndDate;

        if (filterStartDate > filterEndDate)
        {
            (filterStartDate, filterEndDate) = (filterEndDate, filterStartDate);
        }
    }

    private string BuildPresetHref(string range)
    {
        return $"/transactions?accountId={selectedAccountId}&range={Uri.EscapeDataString(range)}";
    }

    private string BuildDetailHref(long transactionId)
    {
        return $"/transactions?accountId={selectedAccountId}&startDate={filterStartDate:yyyy-MM-dd}&endDate={filterEndDate:yyyy-MM-dd}&detail={transactionId}";
    }

    private string BuildCloseHref()
    {
        return $"/transactions?accountId={selectedAccountId}&startDate={filterStartDate:yyyy-MM-dd}&endDate={filterEndDate:yyyy-MM-dd}";
    }

    private Task CloseTransactionDetailAsync()
    {
        selectedReceipt = null;
        DetailTransactionId = null;
        Navigation.NavigateTo(BuildCloseHref(), replace: true);
        return Task.CompletedTask;
    }

    private static string DisplayAccountType(string accountType)
    {
        return accountType == "CHECKING" ? "Харилцах" : accountType;
    }

    private static string DisplayDirection(string direction)
    {
        return direction == "SENT" ? "Илгээсэн" : "Хүлээн авсан";
    }

    private static string DisplayStatus(string status)
    {
        return status == "SUCCESS" ? "Амжилттай" : status;
    }

    private IReadOnlyList<ChartDataPoint> TransactionDirectionChart =>
    [
        new() { Label = "Илгээсэн", Value = transactions.Count(transaction => transaction.Direction == "SENT"), Color = "#dc2626" },
        new() { Label = "Хүлээн авсан", Value = transactions.Count(transaction => transaction.Direction != "SENT"), Color = "#2563eb" }
    ];

    private IReadOnlyList<ChartDataPoint> TransactionStatusChart => transactions
        .GroupBy(transaction => DisplayStatus(transaction.Status))
        .OrderByDescending(group => group.Count())
        .Select((group, index) => new ChartDataPoint
        {
            Label = group.Key,
            Value = group.Count(),
            Color = ChartColors[index % ChartColors.Length]
        })
        .ToList();

    private IReadOnlyList<ChartDataPoint> CashflowByCurrencyChart => transactions
        .Select(transaction => new
        {
            Label = transaction.Direction == "SENT"
                ? $"Зарлага {transaction.SourceCurrency}"
                : $"Орлого {transaction.TargetCurrency}",
            Value = transaction.Direction == "SENT" ? transaction.Amount : transaction.CreditedAmount,
            Color = transaction.Direction == "SENT" ? "#dc2626" : "#2563eb"
        })
        .GroupBy(item => item.Label)
        .OrderBy(group => group.Key)
        .Select(group => new ChartDataPoint
        {
            Label = group.Key,
            Value = group.Sum(item => item.Value),
            Color = group.First().Color
        })
        .ToList();

    private IReadOnlyList<ChartDataPoint> TransactionTrendChart
    {
        get
        {
            var days = Math.Min(31, Math.Max(1, filterEndDate.DayNumber - filterStartDate.DayNumber + 1));
            return Enumerable.Range(0, days)
                .Select(offset => filterStartDate.AddDays(offset))
                .Select(day => new ChartDataPoint
                {
                    Label = day.ToString("MM-dd"),
                    Value = transactions.Count(transaction => DateOnly.FromDateTime(transaction.CreatedAt) == day),
                    Color = "#2563eb"
                })
                .ToList();
        }
    }

    private static readonly string[] ChartColors =
    [
        "#16a34a",
        "#f59e0b",
        "#dc2626",
        "#7c3aed",
        "#0891b2"
    ];
}
