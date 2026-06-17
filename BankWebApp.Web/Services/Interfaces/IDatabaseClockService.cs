using BankWebApp.Web.DTOs.Security;

namespace BankWebApp.Web.Services.Interfaces;

public interface IDatabaseClockService
{
    Task<DatabaseTimeSnapshotDto> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
