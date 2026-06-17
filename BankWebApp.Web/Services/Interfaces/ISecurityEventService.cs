namespace BankWebApp.Web.Services.Interfaces;

public interface ISecurityEventService
{
    Task LogAsync(
        long? userId,
        string? usernameOrEmail,
        string eventType,
        bool success,
        string? message,
        CancellationToken cancellationToken = default);
}
