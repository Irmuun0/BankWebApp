namespace BankWebApp.Web.DTOs.Admin;

public class AdminFxIncomeReportDto
{
    public AdminFxIncomeSummaryDto Summary { get; set; } = new();

    public AdminPagedResultDto<AdminFxIncomeDto> Logs { get; set; } = new();
}
