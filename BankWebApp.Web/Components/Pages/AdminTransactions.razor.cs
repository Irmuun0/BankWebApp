using BankWebApp.Web.DTOs.Admin;
using Microsoft.AspNetCore.Components;

namespace BankWebApp.Web.Components.Pages;

// AdminTransactions.razor нь дэлгэц; энэ partial class нь төлөв, event, өгөгдөл ачаалах логик.
public partial class AdminTransactions
{
    private AdminPagedResultDto<AdminTransactionDto> transactions = new();
    private bool isLoading = true;
    private string? filterValidationError;
    private readonly HashSet<long> expandedDetectionIds = [];

    [SupplyParameterFromQuery] public string? Search { get; set; }
    [SupplyParameterFromQuery] public string? AccountNumber { get; set; }
    [SupplyParameterFromQuery] public string? Username { get; set; }
    [SupplyParameterFromQuery] public string? Currency { get; set; }
    [SupplyParameterFromQuery] public string? Status { get; set; }
    [SupplyParameterFromQuery] public string? SuspiciousStatus { get; set; }
    [SupplyParameterFromQuery] public string? DetectionStatus { get; set; }
    [SupplyParameterFromQuery] public decimal? MinAmount { get; set; }
    [SupplyParameterFromQuery] public decimal? MaxAmount { get; set; }
    [SupplyParameterFromQuery] public DateOnly? StartDate { get; set; }
    [SupplyParameterFromQuery] public DateOnly? EndDate { get; set; }
    [SupplyParameterFromQuery] public int Page { get; set; } = 1;
    [SupplyParameterFromQuery] public int PageSize { get; set; } = 20;

    protected override async Task OnParametersSetAsync()
    {
        isLoading = true;
        filterValidationError = null;
        Page = Math.Max(Page, 1);
        PageSize = NormalizePageSize(PageSize);

        if (MinAmount is not null && MaxAmount is not null && MinAmount > MaxAmount)
        {
            filterValidationError = "Доод дүн нь дээд дүнгээс их байж болохгүй.";
            transactions = new AdminPagedResultDto<AdminTransactionDto>
            {
                Page = Page,
                PageSize = PageSize
            };
            isLoading = false;
            return;
        }

        var filter = new AdminTransactionFilterDto
        {
            Search = Search,
            AccountNumber = AccountNumber,
            Username = Username,
            Currency = Currency,
            Status = Status,
            SuspiciousStatus = SuspiciousStatus,
            DetectionStatus = DetectionStatus,
            MinAmount = MinAmount,
            MaxAmount = MaxAmount,
            StartDate = StartDate,
            EndDate = EndDate
        };

        transactions = await AdminService.GetTransactionsAsync(filter, Page, PageSize);
        isLoading = false;
    }

    private IReadOnlyDictionary<string, string?> BuildPagerQuery()
    {
        return new Dictionary<string, string?>
        {
            ["search"] = Search,
            ["accountNumber"] = AccountNumber,
            ["username"] = Username,
            ["currency"] = Currency,
            ["status"] = Status,
            ["suspiciousStatus"] = SuspiciousStatus,
            ["detectionStatus"] = DetectionStatus,
            ["minAmount"] = FormatDecimal(MinAmount),
            ["maxAmount"] = FormatDecimal(MaxAmount),
            ["startDate"] = FormatDateInput(StartDate),
            ["endDate"] = FormatDateInput(EndDate),
            ["pageSize"] = PageSize.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
    }

    private static string DisplayDetectionStatus(string? status)
    {
        return status switch
        {
            "CHECKED" => "Шалгасан",
            "UNAVAILABLE" => "Service алдаа",
            null or "" => "Шалгаагүй",
            _ => status
        };
    }

    private static string DetectionBadgeClass(string? status)
    {
        return status switch
        {
            "CHECKED" => "text-bg-success",
            "UNAVAILABLE" => "text-bg-warning",
            null or "" => "text-bg-secondary",
            _ => "text-bg-light border"
        };
    }

    private void ToggleDetectionDetails(long transactionId)
    {
        if (!expandedDetectionIds.Add(transactionId))
        {
            expandedDetectionIds.Remove(transactionId);
        }
    }

    private static string? FormatDecimal(decimal? value) => value?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    private static string? FormatDateInput(DateOnly? value) => value?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    private static int NormalizePageSize(int value) => value is 50 or 100 ? value : 20;
}
