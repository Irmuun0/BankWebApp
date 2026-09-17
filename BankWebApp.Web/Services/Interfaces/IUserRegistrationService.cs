using BankWebApp.Web.DTOs.Admin;
using BankWebApp.Web.DTOs.Registration;

namespace BankWebApp.Web.Services.Interfaces;

public interface IUserRegistrationService
{
    Task<(bool Success, string? ErrorMessage)> SubmitRequestAsync(CreateRegistrationRequestDto dto, CancellationToken cancellationToken = default);
    Task<AdminPagedResultDto<AdminRegistrationRequestDto>> GetRequestsAsync(AdminRegistrationRequestFilterDto? filter = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<AdminRegistrationRequestDto?> GetRequestAsync(long requestId, CancellationToken cancellationToken = default);
    Task<RegistrationReviewResultDto> ReviewRequestAsync(long adminUserId, ReviewRegistrationRequestDto dto, CancellationToken cancellationToken = default);
}
