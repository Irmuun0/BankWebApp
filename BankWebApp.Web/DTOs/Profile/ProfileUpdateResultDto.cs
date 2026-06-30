namespace BankWebApp.Web.DTOs.Profile;

public class ProfileUpdateResultDto
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public UserProfileDto? Profile { get; set; }
}
