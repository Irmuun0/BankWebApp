namespace BankWebApp.Web.DTOs.Admin;

public class AdminRegistrationRequestDto
{
    public long Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{LastName}-ийн {FirstName}";
    public string RequestedUsername { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string? EmergencyPhoneNumber { get; set; }
    public string PreferredContactMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? RequestNote { get; set; }
    public string? AdminNote { get; set; }
    public string? DecisionMessage { get; set; }
    public long? CreatedUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
