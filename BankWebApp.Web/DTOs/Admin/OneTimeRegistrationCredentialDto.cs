namespace BankWebApp.Web.DTOs.Admin;

public sealed class OneTimeRegistrationCredentialDto
{
    public long RequestId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string TemporaryPassword { get; init; } = string.Empty;
}
