namespace BankWebApp.Web.DTOs.Admin;

public class UpdateAdminUserProfileDto
{
    public long UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? EmergencyPhoneNumber { get; set; }
}
