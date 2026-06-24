using BankWebApp.Web.Components.Charts;

namespace BankWebApp.Web.DTOs.Admin;

public class AdminSuspiciousDetectionReportDto
{
    public AdminSuspiciousDetectionSummaryDto Summary { get; set; } = new();
    public List<AdminSuspiciousDetectionRuleSummaryDto> RuleSummaries { get; set; } = [];
    public List<ChartDataPoint> DailySuspiciousTrend { get; set; } = [];
    public List<ChartDataPoint> ReviewStatusChart { get; set; } = [];
    public AdminPagedResultDto<AdminSuspiciousDetectionLogDto> Logs { get; set; } = new();
}
