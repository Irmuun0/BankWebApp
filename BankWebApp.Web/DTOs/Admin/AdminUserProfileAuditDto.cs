namespace BankWebApp.Web.DTOs.Admin;

public class AdminUserProfileAuditDto
{
    public long Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string? NewValue { get; set; }
    public string? ActorUsername { get; set; }
    public DateTime CreatedAt { get; set; }
}
