namespace BankWebApp.Web.DTOs.Admin;

public sealed class RegistrationReviewResultDto
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? TemporaryPassword { get; init; }
    public string? Username { get; init; }

    public static RegistrationReviewResultDto Failed(string message) => new()
    {
        Success = false,
        Message = message
    };

    public static RegistrationReviewResultDto Completed(string message) => new()
    {
        Success = true,
        Message = message
    };
}
