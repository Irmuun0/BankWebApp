namespace BankWebApp.Web.DTOs.Auth;

public class PasswordPolicyResultDto
{
    public bool IsValid => Errors.Count == 0;

    public List<string> Errors { get; } = new();
}
