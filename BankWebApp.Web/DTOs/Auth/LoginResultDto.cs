namespace BankWebApp.Web.DTOs.Auth;

public class LoginResultDto
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public long? UserId { get; set; }
    public string? Username { get; set; }
    public string? FullName { get; set; }
    public string? Role { get; set; }
}
