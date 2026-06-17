using BankWebApp.Web.DTOs.Auth;
using BankWebApp.Web.Services.Interfaces;

namespace BankWebApp.Web.Services.Implementations;

public class PasswordPolicyService : IPasswordPolicyService
{
    private const int MinLength = 8;

    public PasswordPolicyResultDto Validate(string password, string? username = null, string? email = null)
    {
        var result = new PasswordPolicyResultDto();

        if (string.IsNullOrWhiteSpace(password))
        {
            result.Errors.Add("Нууц үг хоосон байна.");
            return result;
        }

        if (password.Length < MinLength)
        {
            result.Errors.Add($"Нууц үг хамгийн багадаа {MinLength} тэмдэгттэй байх ёстой.");
        }

        if (!password.Any(char.IsUpper))
        {
            result.Errors.Add("Нууц үг дор хаяж нэг том үсэгтэй байх ёстой.");
        }

        if (!password.Any(char.IsLower))
        {
            result.Errors.Add("Нууц үг дор хаяж нэг жижиг үсэгтэй байх ёстой.");
        }

        if (!password.Any(char.IsDigit))
        {
            result.Errors.Add("Нууц үг дор хаяж нэг тоотой байх ёстой.");
        }

        if (!password.Any(c => !char.IsLetterOrDigit(c)))
        {
            result.Errors.Add("Нууц үг дор хаяж нэг тусгай тэмдэгттэй байх ёстой.");
        }

        if (ContainsUserIdentifier(password, username))
        {
            result.Errors.Add("Нууц үг хэрэглэгчийн нэрийг агуулж болохгүй.");
        }

        var emailName = email?.Split('@', 2)[0];
        if (ContainsUserIdentifier(password, emailName))
        {
            result.Errors.Add("Нууц үг имэйлийн нэр хэсгийг агуулж болохгүй.");
        }

        return result;
    }

    private static bool ContainsUserIdentifier(string password, string? identifier)
    {
        return !string.IsNullOrWhiteSpace(identifier)
            && identifier.Length >= 3
            && password.Contains(identifier, StringComparison.OrdinalIgnoreCase);
    }
}
