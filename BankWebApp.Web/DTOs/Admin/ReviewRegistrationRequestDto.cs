namespace BankWebApp.Web.DTOs.Admin;

public class ReviewRegistrationRequestDto
{
    public long RequestId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string? AdminNote { get; set; }
    public string? DecisionMessage { get; set; }
}
