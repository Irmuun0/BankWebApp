namespace BankWebApp.Web.DTOs.Admin;

public class AdminRegistrationRequestFilterDto
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public DateOnly? CreatedFrom { get; set; }
    public DateOnly? CreatedTo { get; set; }
}
