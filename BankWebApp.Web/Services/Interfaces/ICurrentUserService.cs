using System.Security.Claims;

namespace BankWebApp.Web.Services.Interfaces;

public interface ICurrentUserService
{
    ClaimsPrincipal User { get; }
    bool IsAuthenticated { get; }
    long? UserId { get; }
    string? Username { get; }
    string? FullName { get; }
    string? Role { get; }
    bool IsInRole(string role);
}
