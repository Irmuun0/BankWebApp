using BankWebApp.Web.DTOs.Auth;

namespace BankWebApp.Web.Services.Interfaces;

public interface IAuthService
{
    Task<LoginResultDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
}
