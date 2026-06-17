using BankWebApp.Web.DTOs.Auth;

namespace BankWebApp.Web.Services.Interfaces;

public interface IPasswordPolicyService
{
    PasswordPolicyResultDto Validate(string password, string? username = null, string? email = null);
}
