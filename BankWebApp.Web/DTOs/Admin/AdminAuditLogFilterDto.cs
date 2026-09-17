namespace BankWebApp.Web.DTOs.Admin;

public class AdminAuditLogFilterDto
{
    public string? Source { get; set; }
    public string? Search { get; set; }
    public string? Action { get; set; }
    public string? Actor { get; set; }
    public string? TargetType { get; set; }
    public long? TargetId { get; set; }
    public string? Severity { get; set; }
    public bool? Success { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}
