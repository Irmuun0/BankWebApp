using BankWebApp.Web.DTOs.Health;

namespace BankWebApp.Web.Services.Interfaces;

public interface IOperationalHealthService
{
    Task<OperationalHealthReportDto> CheckAsync(CancellationToken cancellationToken = default);
}
