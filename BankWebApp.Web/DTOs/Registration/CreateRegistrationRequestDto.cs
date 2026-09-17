namespace BankWebApp.Web.DTOs.Registration;

public class CreateRegistrationRequestDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string RequestedUsername { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string? EmergencyPhoneNumber { get; set; }
    public string PreferredContactMethod { get; set; } = "EMAIL";
    public string? RequestNote { get; set; }
}
