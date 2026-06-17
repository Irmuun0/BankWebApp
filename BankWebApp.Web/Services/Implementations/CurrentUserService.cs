using System.Security.Claims;
using BankWebApp.Web.Services.Interfaces;

namespace BankWebApp.Web.Services.Implementations;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public ClaimsPrincipal User => _httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());

    public bool IsAuthenticated => User.Identity?.IsAuthenticated == true;

    public long? UserId
    {
        get
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return long.TryParse(value, out var userId) ? userId : null;
        }
    }

    public string? Username => User.FindFirstValue(ClaimTypes.Name);

    public string? FullName => User.FindFirstValue("FullName");

    public string? Role => User.FindFirstValue(ClaimTypes.Role);

    public bool IsInRole(string role)
    {
        return User.IsInRole(role);
    }
}
