using BankWebApp.Web.Components.Charts;
using BankWebApp.Web.DTOs.Admin;
using BankWebApp.Web.Helpers;
using Microsoft.AspNetCore.Components;

namespace BankWebApp.Web.Components.Pages;

// AdminSuspiciousDetectionReport.razor нь дэлгэц; энэ partial class нь төлөв, event, өгөгдөл ачаалах логик.
public partial class AdminSuspiciousDetectionReport
{
    private AdminSuspiciousDetectionReportDto report = new();
    private AdminSuspiciousDetectionLogDto? selectedLog;
    private bool isLoading = true;
    private DateOnly effectiveStartDate;
    private DateOnly effectiveEndDate;

    [SupplyParameterFromQuery]
    public string? Search { get; set; }

    [SupplyParameterFromQuery]
    public string? StartDate { get; set; }

    [SupplyParameterFromQuery]
    public string? EndDate { get; set; }

    [SupplyParameterFromQuery]
    public string? ReviewStatus { get; set; }

    [SupplyParameterFromQuery]
    public bool SuspiciousOnly { get; set; }

    [SupplyParameterFromQuery]
    public int Page { get; set; } = 1;

    private string StartDateValue => effectiveStartDate.ToString("yyyy-MM-dd");

    private string EndDateValue => effectiveEndDate.ToString("yyyy-MM-dd");

    protected override async Task OnInitializedAsync()
    {
        var today = MongoliaClock.Today;
        effectiveStartDate = TryParseDate(StartDate) ?? today.AddDays(-6);
        effectiveEndDate = TryParseDate(EndDate) ?? today;

        report = await AdminService.GetSuspiciousDetectionReportAsync(
            new AdminSuspiciousDetectionReportFilterDto
            {
                StartDate = effectiveStartDate,
                EndDate = effectiveEndDate,
                Search = Search,
                ReviewStatus = ReviewStatus,
                SuspiciousOnly = SuspiciousOnly
            },
            Page);

        effectiveStartDate = report.Summary.StartDate;
        effectiveEndDate = report.Summary.EndDate;
        isLoading = false;
    }

    private string BuildHref(int page)
    {
        var query = new List<string>
        {
            $"page={page}",
            $"startDate={Uri.EscapeDataString(StartDateValue)}",
            $"endDate={Uri.EscapeDataString(EndDateValue)}"
        };

        if (!string.IsNullOrWhiteSpace(Search))
        {
            query.Add($"search={Uri.EscapeDataString(Search)}");
        }

        if (!string.IsNullOrWhiteSpace(ReviewStatus))
        {
            query.Add($"reviewStatus={Uri.EscapeDataString(ReviewStatus)}");
        }

        if (SuspiciousOnly)
        {
            query.Add("suspiciousOnly=true");
        }

        return $"/admin/suspicious-detection-report?{string.Join("&", query)}";
    }

    private void OpenLogDetail(AdminSuspiciousDetectionLogDto item)
    {
        selectedLog = item;
    }

    private void CloseLogDetail()
    {
        selectedLog = null;
    }

    private string BuildSuspiciousDetailHref(long transactionId)
    {
        return $"/admin/suspicious-transactions/{transactionId}?returnUrl={Uri.EscapeDataString(CurrentReportUrl)}";
    }

    private string CurrentReportUrl => "/" + Navigation.ToBaseRelativePath(Navigation.Uri);

    private static string BuildShortText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        var trimmed = value.Trim();
        return trimmed.Length <= 90 ? trimmed : trimmed[..90] + "...";
    }

    private IReadOnlyList<ChartDataPoint> RuleBreakdownChart => report.RuleSummaries
        .Select((rule, index) => new ChartDataPoint
        {
            Label = rule.RuleCode,
            Value = rule.HitCount,
            Color = ChartColors[index % ChartColors.Length]
        })
        .ToList();

    private static DateOnly? TryParseDate(string? value)
    {
        return DateOnly.TryParse(value, out var parsed) ? parsed : null;
    }

    private static string DisplayServiceStatus(AdminSuspiciousDetectionLogDto item)
    {
        if (!string.Equals(item.ServiceStatus, "CHECKED", StringComparison.OrdinalIgnoreCase))
        {
            return item.ServiceStatus;
        }

        return item.IsSuspicious == true ? "Сэжигтэй" : "Хэвийн";
    }

    private static string GetRiskBadgeClass(decimal riskScore)
    {
        return riskScore >= 80 ? "badge text-bg-danger"
            : riskScore >= 60 ? "badge text-bg-warning"
            : "badge text-bg-light border";
    }

    private static readonly string[] ChartColors =
    [
        "#dc2626",
        "#f59e0b",
        "#2563eb",
        "#16a34a",
        "#7c3aed",
        "#0891b2"
    ];
}
