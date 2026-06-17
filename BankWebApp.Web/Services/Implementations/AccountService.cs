using BankWebApp.Web.Data;
using BankWebApp.Web.Data.Entities;
using BankWebApp.Web.DTOs.Accounts;
using BankWebApp.Web.Helpers;
using BankWebApp.Web.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BankWebApp.Web.Services.Implementations;

public class AccountService : IAccountService
{
    private static readonly string[] SupportedCurrencies = ["MNT", "USD"];

    private readonly BankDbContext _dbContext;

    public AccountService(BankDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<AccountDto>> GetMyAccountsAsync(long userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.UserId == userId)
            .OrderByDescending(account => account.CreatedAt)
            .ThenByDescending(account => account.Id)
            .Select(account => new AccountDto
            {
                Id = account.Id,
                AccountNumber = account.AccountNumber,
                AccountType = account.AccountType,
                Currency = account.Currency,
                Balance = account.Balance,
                IsActive = account.IsActive,
                CreatedAt = account.CreatedAt,
                OwnerName = ((account.User.FirstName ?? "") + " " + (account.User.LastName ?? "")).Trim()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<AccountDto?> GetMyAccountByIdAsync(long userId, long accountId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.UserId == userId && account.Id == accountId)
            .Select(account => new AccountDto
            {
                Id = account.Id,
                AccountNumber = account.AccountNumber,
                AccountType = account.AccountType,
                Currency = account.Currency,
                Balance = account.Balance,
                IsActive = account.IsActive,
                CreatedAt = account.CreatedAt,
                OwnerName = ((account.User.FirstName ?? "") + " " + (account.User.LastName ?? "")).Trim(),
                LastTransactionAt = _dbContext.Transactions
                    .Where(transaction => transaction.FromAccountId == account.Id || transaction.ToAccountId == account.Id)
                    .Max(transaction => (DateTime?)transaction.CreatedAt)
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AccountSummaryDto> GetMyAccountSummaryAsync(long userId, CancellationToken cancellationToken = default)
    {
        var accounts = await _dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.UserId == userId)
            .Select(account => new
            {
                account.Currency,
                account.Balance,
                account.IsActive
            })
            .ToListAsync(cancellationToken);

        return new AccountSummaryDto
        {
            TotalAccounts = accounts.Count,
            TotalMntBalance = accounts.Where(account => account.Currency == "MNT").Sum(account => account.Balance),
            TotalUsdBalance = accounts.Where(account => account.Currency == "USD").Sum(account => account.Balance),
            ActiveAccountCount = accounts.Count(account => account.IsActive),
            InactiveAccountCount = accounts.Count(account => !account.IsActive)
        };
    }

    public async Task<(bool Success, string? ErrorMessage, AccountDto? Account)> OpenAccountAsync(
        long userId,
        CreateAccountDto dto,
        CancellationToken cancellationToken = default)
    {
        var currency = dto.Currency.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(currency))
        {
            return (false, "Валют сонгоно уу.", null);
        }

        if (!SupportedCurrencies.Contains(currency))
        {
            return (false, "Зөвхөн MNT эсвэл USD валюттай данс нээх боломжтой.", null);
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var accountNumber = GenerateAccountNumber();
            var exists = await _dbContext.Accounts
                .AnyAsync(account => account.AccountNumber == accountNumber, cancellationToken);

            if (exists)
            {
                continue;
            }

            var now = MongoliaClock.Now;
            var account = new Account
            {
                UserId = userId,
                AccountNumber = accountNumber,
                AccountType = "CHECKING",
                Currency = currency,
                Balance = 0,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };

            _dbContext.Accounts.Add(account);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                return (true, null, MapToDto(account));
            }
            catch (DbUpdateException)
            {
                _dbContext.Entry(account).State = EntityState.Detached;
            }
        }

        return (false, "Данс нээх үед алдаа гарлаа.", null);
    }

    public async Task<(bool Success, string? ErrorMessage)> SetMyAccountStatusAsync(
        long userId,
        long accountId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(account => account.UserId == userId && account.Id == accountId, cancellationToken);

        if (account is null)
        {
            return (false, "Данс олдсонгүй эсвэл та энэ дансыг өөрчлөх эрхгүй байна.");
        }

        account.IsActive = isActive;
        account.UpdatedAt = MongoliaClock.Now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (true, isActive ? "Данс идэвхтэй боллоо." : "Данс идэвхгүй боллоо.");
    }

    private static string GenerateAccountNumber()
    {
        return $"10{Random.Shared.Next(0, 100_000_000):D8}";
    }

    private static AccountDto MapToDto(Account account)
    {
        return new AccountDto
        {
            Id = account.Id,
            AccountNumber = account.AccountNumber,
            AccountType = account.AccountType,
            Currency = account.Currency,
            Balance = account.Balance,
            IsActive = account.IsActive,
            CreatedAt = account.CreatedAt
        };
    }
}
