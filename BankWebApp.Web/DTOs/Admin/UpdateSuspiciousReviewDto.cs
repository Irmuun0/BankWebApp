namespace BankWebApp.Web.DTOs.Admin;

public class UpdateSuspiciousReviewDto
{
    public long TransactionId { get; set; }
    public string ReviewStatus { get; set; } = string.Empty;
    public string? ReviewNote { get; set; }
    public bool SendUserNotification { get; set; }
    public string? UserNotificationMessage { get; set; }
    public bool NotifySender { get; set; }
    public bool NotifyReceiver { get; set; }
    public string? SenderNotificationMessage { get; set; }
    public string? ReceiverNotificationMessage { get; set; }
    public bool DeactivateSenderAccount { get; set; }
    public bool DeactivateReceiverAccount { get; set; }
    public bool DeactivateSenderUser { get; set; }
    public bool DeactivateReceiverUser { get; set; }
    public long? ExpectedUpdatedAtTicks { get; set; }
}
