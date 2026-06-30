using BankWebApp.Web.DTOs.Transactions;

namespace BankWebApp.Web.Services.Interfaces;

public interface ITransactionService
{
    Task<List<UserTransactionDto>> GetMyTransactionsAsync(long currentUserId, CancellationToken cancellationToken = default);
    Task<List<UserTransactionDto>> GetMyTransactionsAsync(long currentUserId, DateOnly? startDate, DateOnly? endDate, int count = 500, CancellationToken cancellationToken = default);
    Task<List<UserTransactionDto>> GetMyTransactionsAsync(long currentUserId, long accountId, DateOnly? startDate, DateOnly? endDate, CancellationToken cancellationToken = default);
    Task<List<UserTransactionDto>> GetRecentMyTransactionsAsync(long currentUserId, int count = 5, CancellationToken cancellationToken = default);
    Task<List<UserTransactionDto>> GetRecentMyTransactionsAsync(long currentUserId, long accountId, int count = 5, CancellationToken cancellationToken = default);
    Task<(bool Success, string? ErrorMessage, long? TransactionId)> CreateTransactionAsync(long currentUserId, CreateTransactionDto dto, CancellationToken cancellationToken = default);
    Task<ReceiverAccountPreviewDto?> GetReceiverAccountPreviewAsync(long currentUserId, string accountNumber, CancellationToken cancellationToken = default);
    Task<TransactionReceiptDto?> GetMyTransactionReceiptAsync(long currentUserId, long transactionId, CancellationToken cancellationToken = default);
}
