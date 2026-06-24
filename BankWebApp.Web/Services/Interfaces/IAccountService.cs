using BankWebApp.Web.DTOs.Accounts;

namespace BankWebApp.Web.Services.Interfaces;

public interface IAccountService
{
    Task<List<AccountDto>> GetMyAccountsAsync(long userId, CancellationToken cancellationToken = default);
    Task<AccountDto?> GetMyAccountByIdAsync(long userId, long accountId, CancellationToken cancellationToken = default);
    Task<AccountSummaryDto> GetMyAccountSummaryAsync(long userId, CancellationToken cancellationToken = default);
    Task<(bool Success, string? ErrorMessage, AccountDto? Account)> OpenAccountAsync(long userId, CreateAccountDto dto, CancellationToken cancellationToken = default);
    Task<(bool Success, string? ErrorMessage)> SetMyAccountStatusAsync(long userId, long accountId, bool isActive, CancellationToken cancellationToken = default);
    Task<(bool Success, string? ErrorMessage)> SetPrimaryAccountAsync(long userId, long accountId, CancellationToken cancellationToken = default);
}
