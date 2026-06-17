using BankWebApp.Web.DTOs.Ai;

namespace BankWebApp.Web.Services.Interfaces;

public interface IAiDetectionService
{
    Task<SuspiciousDetectionResultDto?> DetectSuspiciousAsync(
        SuspiciousDetectionRequestDto request,
        CancellationToken cancellationToken = default);
}
