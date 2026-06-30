using BankWebApp.Web.DTOs.Profile;

namespace BankWebApp.Web.Services.Interfaces;

public interface IProfileService
{
    Task<UserProfileDto?> GetProfileAsync(long userId, CancellationToken cancellationToken = default);
    Task<ProfileUpdateResultDto> UpdateProfileAsync(long userId, UpdateProfileDto dto, CancellationToken cancellationToken = default);
}
