namespace BankWebApp.Web.DTOs.Admin;

public class UpdateSuspiciousReviewDto
{
    public long TransactionId { get; set; }
    public string ReviewStatus { get; set; } = string.Empty;
    public string? ReviewNote { get; set; }
    public bool SendUserNotification { get; set; }
    public string? UserNotificationMessage { get; set; }
    public long? ExpectedUpdatedAtTicks { get; set; }
}
